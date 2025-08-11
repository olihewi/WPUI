using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;

namespace Pokemon.Importers.DS.Narc.Entries
{
    public class CellsNarcEntry : NarcEntry
    {
        private static HashSet<string> Magic = new(){ "NCER", "RECN" };
        // Parsed cells
        public IReadOnlyList<Cell> Cells => _cells;
        private readonly List<Cell> _cells = new();
        
        // CEBK diagnostics
        public int CebkOffset { get; private set; } = -1;  // absolute within NCER/RECN payload (for info)
        public int CebkLength { get; private set; } = -1;

        // Parsed CEBK header
        public ushort CellCount { get; private set; }
        public ushort BankAttributes { get; private set; }
        public int CellDataPtr { get; private set; }        // relative to start of CEBK payload
        public uint MappingMode { get; private set; }       // 0..4 (1D 32K/64K/128K/256K, 2D)
        public int VramXferPtr { get; private set; }        // relative; optional
        public int StringBankPtr { get; private set; }      // relative; optional
        public int UserExtPtr { get; private set; }         // relative; optional
        
        public CellsNarcEntry(int index, [CanBeNull] string name, int offset, int length, byte[] fileData) : base(index, name, offset, length, fileData)
        {
            using var ms = new MemoryStream(fileData, Offset, Length, writable: false);
            using var br = new BinaryReader(ms, System.Text.Encoding.ASCII, leaveOpen: true);

            var hdr = NarcFile.ReadNitroHeader(br);
            if (!Magic.Contains(hdr.Magic))
                throw new InvalidDataException($"Not an NCER entry: {hdr.Magic}");

            // Walk blocks; look for KBEC (CEBK)
            for (int b = 0; b < hdr.BlockCount; b++)
            {
                string fourCC = NarcFile.ReadFourCC(br);
                uint blkSize = br.ReadUInt32();
                long blkStart = ms.Position;

                if (fourCC == "KBEC") // CEBK
                {
                    CebkOffset = (int)blkStart;
                    CebkLength = (int)(blkSize - 8);

                    byte[] payload = br.ReadBytes(CebkLength);
                    try
                    {
                        ParseCebk(payload);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                else
                {
                    ms.Position = blkStart + blkSize - 8;
                }
            }

            if (_cells.Count == 0)
                Debug.LogException(new InvalidDataException("NCER/RECN had no KBEC/CEBK cell bank."));
        }
        private void ParseCebk(byte[] cebk)
        {
            // CEBK header per spec:
            // 0x00 u16 cellCount
            // 0x02 u16 bankAttributes (bit 0 => cells include bounding rects)
            // 0x04 u32 cellDataPtr (relative to start of CEBK)
            // 0x08 u32 mappingMode (0..4)
            // 0x0C u32 vramTransferPtr (relative) or 0
            // 0x10 u32 stringBankPtr (relative) or 0
            // 0x14 u32 userExtPtr (relative) or 0
            if (cebk.Length < 0x18)
                throw new InvalidDataException("CEBK block too small");

            CellCount       = BitConverter.ToUInt16(cebk, 0x00);
            BankAttributes  = BitConverter.ToUInt16(cebk, 0x02);
            CellDataPtr     = BitConverter.ToInt32(cebk, 0x04);
            MappingMode     = BitConverter.ToUInt32(cebk, 0x08);
            VramXferPtr     = BitConverter.ToInt32(cebk, 0x0C);
            StringBankPtr   = BitConverter.ToInt32(cebk, 0x10);
            UserExtPtr      = BitConverter.ToInt32(cebk, 0x14);

            if (CellCount == 0 || CellCount > 4096)
                throw new InvalidDataException($"Unreasonable cell count: {CellCount}");
            if (CellDataPtr < 0 || CellDataPtr >= cebk.Length)
                throw new InvalidDataException("Invalid cellDataPtr in CEBK");

            bool hasBBoxes = (BankAttributes & 0x0001) != 0;
            int cellRecSize = hasBBoxes ? 16 : 8;

            // Sanity for cell table
            int cellTableEnd = CellDataPtr + (cellRecSize * CellCount);
            if (cellTableEnd > cebk.Length)
                throw new InvalidDataException("Cell table exceeds CEBK size");

            // OAM data base: “starts immediately after the cell array” — align to 4 bytes just in case.
            int oamBase = cellTableEnd;

            _cells.Clear();
            for (int i = 0; i < CellCount; i++)
            {
                int p = CellDataPtr + i * cellRecSize;

                ushort oamCount = BitConverter.ToUInt16(cebk, p + 0x00);
                ushort cellAttrs = BitConverter.ToUInt16(cebk, p + 0x02);
                int oamPtrRel = BitConverter.ToInt32(cebk, p + 0x04); // relative to OAM base

                // Bounds (if present): MaxX, MaxY, MinX, MinY (s16)
                short maxX = 0, maxY = 0, minX = 0, minY = 0;
                if (hasBBoxes)
                {
                    maxX = BitConverter.ToInt16(cebk, p + 0x08);
                    maxY = BitConverter.ToInt16(cebk, p + 0x0A);
                    minX = BitConverter.ToInt16(cebk, p + 0x0C);
                    minY = BitConverter.ToInt16(cebk, p + 0x0E);
                }
                else
                {
                    // If no bbox, we’ll compute a tight box from OAM later (optional). For now, provide a minimal box.
                    minX = minY = 0;
                    maxX = maxY = 1;
                }

                int oamListPos = oamBase + oamPtrRel;

                //if (oamListPos < 0 || oamListPos + oamBytes > cebk.Length)
                //    throw new InvalidDataException($"OAM list for cell {i} exceeds CEBK size");

                var cell = new Cell
                {
                    Index = i,
                    Bounds = BoundsFromMinMax(minX, minY, maxX, maxY),
                    Oams = new Oam[oamCount],
                    OamsPtr = oamPtrRel,
                };

                int q = oamListPos;
                for (int j = 0; j < oamCount; j++)
                {
                    ushort attr0 = BitConverter.ToUInt16(cebk, q + 0);
                    ushort attr1 = BitConverter.ToUInt16(cebk, q + 2);
                    ushort attr2 = BitConverter.ToUInt16(cebk, q + 4);
                    int stride = (q + 8 > cebk.Length || BitConverter.ToUInt16(cebk, q + 6) != 0) ? 6 : 8;
                    q += stride;

                    var oam = DecodeOam(attr0, attr1, attr2);
                    
                    cell.Oams[j] = oam;
                }

                if (!hasBBoxes && cell.Oams.Length > 0)
                {
                    var oam = cell.Oams[0];
                    cell.Bounds = new RectInt(oam.X, oam.Y, oam.Width, oam.Height);
                    for (int j = 1; j < cell.Oams.Length; j++)
                    {
                        oam = cell.Oams[j];
                        cell.Bounds.xMin = Mathf.Min(cell.Bounds.xMin, oam.X);
                        cell.Bounds.yMin = Mathf.Min(cell.Bounds.yMin, oam.X);
                        cell.Bounds.xMax = Mathf.Max(cell.Bounds.xMax, oam.X + oam.Width);
                        cell.Bounds.yMax = Mathf.Max(cell.Bounds.yMax, oam.Y + oam.Height);
                    }
                }

                _cells.Add(cell);
            }
        }
        
        private static RectInt BoundsFromMinMax(short minX, short minY, short maxX, short maxY)
        {
            // NCER docs name Max then Min; normalize to min..max.
            if (maxX < minX) (minX, maxX) = (maxX, minX);
            if (maxY < minY) (minY, maxY) = (maxY, minY);

            int w = Math.Max(1, maxX - minX);
            int h = Math.Max(1, maxY - minY);
            return new RectInt(minX, minY, w, h);
        }

        private static Oam DecodeOam(ushort attr0, ushort attr1, ushort attr2)
        {
            // 0: SS D M OO P R YYYYYYYY
            // S -> Shape (0=square, 1=horizontal, 2=vertical, 3=prohibited)
            // D -> Palette size (0=16/16, 1=256/1)
            // M -> Mosaic on/off
            // O -> Mode (0=normal, 1=semi-transparent, 2=obj window, 3=prohibited)
            // P -> if R: Double size (0=normal, 1=double)
            //      else: Hidden (0=shown, 1=hidden)
            // R -> Rotation or scale on/off
            // Y -> Y Coordinate
            
            // 1: SS PPPPP XXXXXXXXX
            // S -> Size (See table)
            // P -> if R: rotation params
            //      else: 0=normal, 12=horizontal flip, 13=vertical flip
            // X -> X Coordinate
            
            // 2: IIII PP TTTTTTTTTT
            // I -> Palette ID (unused in D=0)
            // P -> Priority relative to background (0 high)
            // T -> Offset of the tiles
            
            
            uint rawY = attr0 & 0x00FFu;
            uint rawX = attr1 & 0x01FFu;
            int y = (int)((rawY >= 128) ? rawY - 256 : rawY);
            int x = (int)((rawX >= 256) ? rawX - 512 : rawX);

            int shape = (attr0 >> 14) & 0x03;
            int size = (attr1 >> 14) & 0x03;

            (int w, int h) = GetObjDimensions(shape, size);

            bool rotscale = ((attr0 >> 8) & 0x01) != 0;
            bool hFlip = !rotscale && (((attr1 >> 12) & 0x01) != 0);
            bool vFlip = !rotscale && (((attr1 >> 13) & 0x01) != 0);

            int tileIndex = attr2 & 0x03FF;
            int priority = (attr2 >> 10) & 0x03;
            int palBank = (attr2 >> 12) & 0x0F;
            bool use8bpp = ((attr0 >> 13) & 0x01) != 0;

            return new Oam
            {
                Attr0 = attr0,
                Attr1 = attr1,
                Attr2 = attr2,
                X = x,
                Y = y,
                Width = w,
                Height = h,
                HFlip = hFlip,
                VFlip = vFlip,
                Shape = shape,
                Size = size,
                TileIndex = tileIndex,
                PaletteBank = palBank,
                Use8Bpp = use8bpp,
                Priority = priority
            };
        }

        private static (int w, int h) GetObjDimensions(int shape, int size)
        {
            // Shape _________\___0___\___1___\___2___\___3___\
            // 0 Square ______|  8x8  | 16x16 | 32x32 | 64x64 |
            // 1 Horizontal __\ 16x8  \ 32x8  \ 32x16 \ 64x32 \
            // 2 Vertical ____|  8x16 |  8x32 | 16x32 | 32x64 |
            // 3 Invalid _____\______________8x8______________\
            
            return shape switch
            {
                0 => size switch { 0 => (8, 8), 1 => (16, 16), 2 => (32, 32), 3 => (64, 64), _ => (8, 8) },
                1 => size switch { 0 => (16, 8), 1 => (32, 8), 2 => (32, 16), 3 => (64, 32), _ => (16, 8) },
                2 => size switch { 0 => (8, 16), 1 => (8, 32), 2 => (16, 32), 3 => (32, 64), _ => (8, 16) },
                _ => (8, 8)
            };
        }
        
        
        public sealed class Cell
        {
            public int Index { get; internal set; }
            public RectInt Bounds; // Relative bounds (xMin,yMin,width,height), origin at cell pivot
            public Oam[] Oams { get; set; }
            public int OamsPtr { get; set; }

            // Render the cell into a new texture. Returns texture and the pivot (offset applied).
            public (Texture2D texture, Vector2Int pivot) Render(TilesNarcEntry tiles, IReadOnlyList<Color32> palette, bool pointFilter = true, int transparentIndex = 0)
            {
                if (tiles == null) throw new ArgumentNullException(nameof(tiles));
                if (palette == null) throw new ArgumentNullException(nameof(palette));

                // Canvas size derived from cell bounds
                int width = Math.Max(1, Bounds.width);
                int height = Math.Max(1, Bounds.height);
                var canvas = new Color32[width * height];

                // Precompute pivot so OAM placements align on canvas
                var pivot = new Vector2Int(-Bounds.xMin, -Bounds.yMin);

                Debug.Log($"rect: {Bounds}");
                for (var oamIndex = Oams.Length - 1; oamIndex >= 0; oamIndex--)
                {
                    var o = Oams[oamIndex];
                    Debug.Log(
                        $"x: {o.X} y: {o.Y} w: {o.Width} h: {o.Height}\nshape:{o.Shape} size: {o.Size} tileIndex {o.TileIndex}");
                    // Determine bpp (prefer tiles’ detected bpp; allow per-OAM override if Attr0 says otherwise)
                    int bpp = o.Use8Bpp ? 8 : tiles.BitsPerPixel;

                    // Select palette for this OAM
                    IReadOnlyList<Color32> palForOam = palette;
                    if (bpp == 4)
                    {
                        if (palette.Count >= 256)
                        {
                            // Slice subpalette by palette bank (Attr2[15:12])
                            palForOam = SlicePaletteBank(palette, o.PaletteBank);
                        }
                        else if (palette.Count < 16)
                        {
                            throw new ArgumentException(
                                "4bpp requires at least 16 colors or a 256-color master palette.", nameof(palette));
                        }
                    }
                    else // 8bpp
                    {
                        if (palette.Count < 256)
                            throw new ArgumentException("8bpp requires a 256-color palette.", nameof(palette));
                    }

                    // Tiles across this OAM sprite
                    int tilesX = o.Width / 8;
                    int tilesY = o.Height / 8;

                    for (int ty = 0; ty < tilesY; ty++)
                    {
                        for (int tx = 0; tx < tilesX; tx++)
                        {
                            int localTx = o.HFlip ? (tilesX - 1 - tx) : tx;
                            int localTy = o.VFlip ? (tilesY - 1 - ty) : ty;

                            int tileOffset = localTy * tiles.WidthTiles /* tilesX */ + localTx;
                            int tileIndex = o.TileIndex + tileOffset;

                            Color32[] tilePixels = (bpp == 8)
                                ? tiles.DecodeTile8Bpp(tileIndex, palForOam)
                                : tiles.DecodeTile4Bpp(tileIndex, palForOam);

                            // Destination top-left for this 8x8 tile
                            int dstX = pivot.x + o.X + tx * 8;
                            int dstY = pivot.y + o.Y + ty * 8;

                            // Blit 8x8 with vertical flip already handled by localTy
                            for (int py = 0; py < 8; py++)
                            {
                                int canvasY = height - 1 - (dstY + py);
                                if (canvasY < 0 || canvasY >= height) continue;

                                for (int px = 0; px < 8; px++)
                                {
                                    int canvasX = dstX + px;
                                    if (canvasX < 0 || canvasX >= width) continue;

                                    // If we applied flips by swapping tile indices, per-pixel order is normal
                                    var src = tilePixels[py * 8 + px];
                                    if (src.a == 0) continue;
                                    canvas[canvasY * width + canvasX] = src;
                                }
                            }
                        }
                    }
                }

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false);
                tex.filterMode = pointFilter ? FilterMode.Point : FilterMode.Bilinear;
                tex.SetPixels32(canvas);
                tex.Apply(false, false);
                return (tex, pivot);
            }

            private static IReadOnlyList<Color32> SlicePaletteBank(IReadOnlyList<Color32> master256, int bank)
            {
                int start = Math.Clamp(bank, 0, 15) * 16;
                var slice = new Color32[16];
                for (int i = 0; i < 16; i++) slice[i] = master256[start + i];
                return slice;
            }
        }

        public sealed class Oam
        {
            // Raw attributes
            public ushort Attr0;
            public ushort Attr1;
            public ushort Attr2;

            // Decoded
            public int X;                 // relative to cell origin (can be negative)
            public int Y;                 // relative to cell origin (can be negative)
            public int Width;             // in pixels
            public int Height;            // in pixels
            public bool HFlip;
            public bool VFlip;
            public int Shape;             // 0 square, 1 horizontal, 2 vertical
            public int Size;              // 0..3
            public int TileIndex;         // base 8x8 tile index
            public int PaletteBank;       // for 4bpp: Attr2[15:12]
            public bool Use8Bpp;          // Attr0 bit 13
            public int Priority;          // Attr2[11:10]
        }
    }
}