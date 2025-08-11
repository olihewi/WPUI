using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;

namespace Pokemon.Importers.DS.Narc.Entries
{
    public class TilesNarcEntry : NarcEntry
    {
        private static HashSet<string> Magic = new(){ "NCGR", "RGCN" };
        public byte[] RawTileData { get; private set; } = Array.Empty<byte>();
        public int WidthTiles { get; private set; }
        public int HeightTiles { get; private set; }
        
        // Auto-detected bits-per-pixel for tiles: 4 or 8.
        public int BitsPerPixel { get; private set; } = 4;
        public enum Mapping : uint
        {
            TwoD = 0x000000,
            OneD32k = 0x000010,
            OneD64k = 0x100010,
            OneD128k = 0x200010,
            OneD256k = 0x300010,
        }
        public Mapping MappingMode { get; private set; }
        public bool IsLinealLayout { get; private set; }
        
        public TilesNarcEntry(int index, [CanBeNull] string name, int offset, int length, byte[] fileData) 
            : base(index, name, offset, length, fileData)
        {
            using var ms = new MemoryStream(fileData, offset, length, writable: false);
            using var br = new BinaryReader(ms, System.Text.Encoding.ASCII, leaveOpen: true);

            var hdr = NarcFile.ReadNitroHeader(br);
            if (!Magic.Contains(hdr.Magic))
                throw new InvalidDataException($"Not an NCGR entry: {hdr.Magic}");

            // Walk blocks; find CHAR/RAHC
            for (int b = 0; b < hdr.BlockCount; b++)
            {
                string blkMagic = NarcFile.ReadFourCC(br);
                uint blkSize = br.ReadUInt32();
                long blkStart = ms.Position;

                if (blkMagic == "RAHC" || blkMagic == "CHAR")
                {
                    // Read entire CHAR block payload into memory for subheader parsing.
                    byte[] charPayload = br.ReadBytes((int)blkSize - 8);

                    HeightTiles = BitConverter.ToUInt16(charPayload, 0x00);
                    WidthTiles = BitConverter.ToUInt16(charPayload, 0x02);
                    BitsPerPixel = BitConverter.ToUInt32(charPayload, 0x04) == 3 ? 4 : 8;
                    MappingMode = (Mapping)BitConverter.ToUInt32(charPayload, 0x08);
                    IsLinealLayout = (BitConverter.ToUInt32(charPayload, 0x0C) & 0x01) == 0x01;
                    var dataSize = BitConverter.ToUInt32(charPayload, 0x10);
                    var graphicsDataPtr = BitConverter.ToUInt32(charPayload, 0x14);

                    RawTileData = new byte[dataSize];
                    Buffer.BlockCopy(charPayload, (int)graphicsDataPtr, RawTileData, 0, (int)dataSize);
                    if (ToString().StartsWith("0: 22_00"))
                    {
                        Debug.Log(
                            $"width {WidthTiles} height {HeightTiles} bpp {BitsPerPixel} mappingMode {MappingMode} isLinealLayout {IsLinealLayout}\n");
                        
                        var hexBuilder = new StringBuilder(RawTileData.Length * 2);

                        foreach (byte by in RawTileData)
                        {
                            hexBuilder.AppendFormat("{0:X2}", by);
                        }

                        string hexString = hexBuilder.ToString();
                        Debug.Log(hexString);
                    }
                    return;
                }
                else
                {
                    // Skip unknown/other blocks
                    ms.Position = blkStart + blkSize - 8;
                }
            }

            throw new InvalidDataException("NCGR/RGCN entry had no RAHC/CHAR block.");
        }

        // Auto-decoding based on detected bpp
        public Color32[] DecodeTile(int tileIndex, IReadOnlyList<Color32> palette)
        {
            if (BitsPerPixel == 8) return DecodeTile8Bpp(tileIndex, palette);
            return DecodeTile4Bpp(tileIndex, palette);
        }

        public Texture2D BuildTileSheet(IReadOnlyList<Color32> palette, bool pointFilter = true)
        {
            int tileCount = WidthTiles * HeightTiles;
            int width = WidthTiles * 8;
            int height = HeightTiles * 8;
            int pixelCount = width * height;
            
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.filterMode = pointFilter ? FilterMode.Point : FilterMode.Bilinear;

            var buf = new Color32[pixelCount];

            for (int t = 0; t < tileCount; t++)
            {
                int tx = (t % WidthTiles) * 8;
                int ty = (t / WidthTiles) * 8;
                var tile = DecodeTile(t, palette);

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        buf[(tx + x) + (height - 1 - (ty + y)) * width] = tile[y * 8 + x];
                    }
                }
            }

            tex.SetPixels32(buf);
            tex.Apply(false, false);
            return tex;
        }

        // Existing decoders remain the same
        public Color32[] DecodeTile4Bpp(int tileIndex, IReadOnlyList<Color32> palette)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (palette.Count < 16) throw new ArgumentException("4bpp requires a 16-color palette.", nameof(palette));

            int rowOffset = IsLinealLayout ? WidthTiles * 4 : 4;
            int baseOffset = IsLinealLayout 
                ? tileIndex % WidthTiles * 4
                  + tileIndex / WidthTiles * rowOffset * 8
                : tileIndex * 32;

            var pixels = new Color32[8 * 8];
            int p = 0;
            for (int row = 0; row < 8; row++)
            {
                for (int colByte = 0; colByte < 4; colByte++)
                {
                    byte packed = RawTileData[baseOffset + row * rowOffset + colByte];
                    int lo = packed & 0x0F;
                    int hi = (packed >> 4) & 0x0F;

                    pixels[p++] = palette[lo];
                    pixels[p++] = palette[hi];
                }
            }
            return pixels;
        }

        public Color32[] DecodeTile8Bpp(int tileIndex, IReadOnlyList<Color32> palette)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (palette.Count < 256) throw new ArgumentException("8bpp requires a 256-color palette.", nameof(palette));

            int rowOffset = IsLinealLayout ? WidthTiles * 8 : 8;
            int baseOffset = IsLinealLayout
                ? tileIndex % WidthTiles * 8
                  + tileIndex / WidthTiles * rowOffset * 8
                : tileIndex * 64;

            var pixels = new Color32[8 * 8];
            for (int i = 0; i < 64; i++)
            {
                int idx = RawTileData[baseOffset + i];
                pixels[i] = palette[idx];
            }
            return pixels;
        }
    }
}