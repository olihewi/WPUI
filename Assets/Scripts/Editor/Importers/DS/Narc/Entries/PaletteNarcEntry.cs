using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;

namespace Pokemon.Importers.DS.Narc.Entries
{
    public sealed class PaletteNarcEntry : NarcEntry
    {
        private static HashSet<string> Magic = new() { "NCLR", "RLCN",};
        public IReadOnlyList<Color32[]>Palettes => _palettes;
        public int TotalColors;
        public int ColorsPerPalette { get; private set; } // Common: 16 for 4bpp, 256 for 8bpp (but can vary)
        private readonly List<Color32[]> _palettes = new();
        
        public PaletteNarcEntry(int index, [CanBeNull] string name, int offset, int length, byte[] fileData, bool zeroIsTransparent = true) 
            : base(index, name, offset, length, fileData)
        {
            using var ms = new MemoryStream(fileData, offset, length, writable: false);
            using var br = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);
            
            // Nitro header
            var hdr = NarcFile.ReadNitroHeader(br);
            if (!Magic.Contains(hdr.Magic))
                throw new InvalidDataException($"Not an NCLR entry: {hdr.Magic}");
            
            // Iterate blocks to find PLTT/TTLP
            long fileStart = ms.Position - 12; // header already read
            for (int b = 0; b < hdr.BlockCount; b++)
            {
                string blkMagic = NarcFile.ReadFourCC(br);
                uint blkSize = br.ReadUInt32();
                long blkStart = ms.Position;

                if (blkMagic == "TTLP" || blkMagic == "PLTT")
                {
                    // Palette data follows; structure varies between games.
                    // We'll treat the rest of this block as raw 16-bit colors.
                    int payload = (int)(blkSize - 8);
                    var raw = br.ReadBytes(payload);

                    int colorCount = BitConverter.ToInt32(raw, 0x08) / 2;
                    int blkOffset = BitConverter.ToInt32(raw, 0x0C);
                    TotalColors = colorCount;

                    // Heuristic for ColorsPerPalette:
                    // If TotalColors is multiple of 16 but not 256 => assume 16.
                    // If multiple of 256 => prefer 256 (common for 8bpp pages).
                    if (colorCount % 256 == 0)
                        ColorsPerPalette = 256;
                    else if (colorCount % 16 == 0)
                        ColorsPerPalette = 16;
                    else
                        ColorsPerPalette = colorCount; // single custom-sized palette

                    int paletteCount = Math.Max(1, colorCount / ColorsPerPalette);

                    _palettes.Clear();
                    for (int p = 0; p < paletteCount; p++)
                    {
                        var pal = new Color32[ColorsPerPalette];
                        for (int i = 0; i < ColorsPerPalette; i++)
                        {
                            int idx = blkOffset + 2 * (p * ColorsPerPalette + i);
                            ushort c = BitConverter.ToUInt16(raw, idx);
                            pal[i] = NarcFile.Bgr555ToColor32(c, zeroIsTransparent, p, i);
                        }
                        _palettes.Add(pal);
                    }

                    // Done after PLTT
                    return;
                }
                // Skip unknown block
                ms.Position = blkStart + blkSize - 8;
            }

            throw new InvalidDataException("NCLR/RLCN entry had no PLTT/TTLP block.");
        }
    }
}