using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Pokemon.Importers.DS.Narc.Entries;
using UnityEngine;

namespace Pokemon.Importers.DS.Narc
{
    public sealed class NarcFile
    {
        // Header
        public string Magic { get; private set; } = "NARC";
        public ushort ByteOrder { get; private set; } // Typically 0xFFFE (LE)
        public ushort Version { get; private set; }
        public uint FileSize { get; private set; }
        public ushort HeaderSize { get; private set; }
        public ushort BlockCount { get; private set; }

        // Entries extracted from FAT (BTAF/FATB)
        public IReadOnlyList<NarcEntry> Entries => _entries;
        private readonly List<NarcEntry> _entries = new();
        
        
        // Backing data of the FIMG/GMIF block.
        private byte[] _fileData = Array.Empty<byte>();
        private long _fileDataBaseOffset; // absolute offset within the stream where fileData starts (for validation)
        
        // Optional names (flat list). Many NARCs don’t include names; indices are then the only identifiers.
        public IReadOnlyList<string>? Names { get; private set; }
        
        private NarcFile() { }

        public static NarcFile Parse(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new ArgumentException("Stream must be readable.", nameof(input));

            var narc = new NarcFile();
            using var br = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);
            var magic = ReadFourCC(br);
            if (magic != "NARC")
                throw new InvalidDataException($"Invalid NARC magic: {magic}");
            
            narc.Magic = magic;
            narc.ByteOrder = br.ReadUInt16();   // Usually 0xFFFE for little-endian
            narc.Version = br.ReadUInt16();     // e.g., 0x0100
            narc.FileSize = br.ReadUInt32();    // Total file size per header
            narc.HeaderSize = br.ReadUInt16();  // Usually 0x0010
            narc.BlockCount = br.ReadUInt16();  // Typically 3 (FAT/FNT/FIMG)
            
            // --- Blocks ---
            // We’ll parse in the order they appear; typical order: FAT(BTAF) -> FNT(BTNF) -> FIMG(GMIF).
            // But we’ll be resilient and accept either order, provided we see all relevant parts.
            var fatEntries = new List<(uint start, uint end)>();
            byte[]? fntRaw = null;

            for (int b = 0; b < narc.BlockCount; b++)
            {
                long blockStart = input.Position;
                string blockId = ReadFourCC(br);
                uint blockSize = br.ReadUInt32();

                switch (blockId)
                {
                    case "BTAF":
                    case "FATB":
                        ParseFat(br, blockStart, blockSize, fatEntries);
                        break;
                    
                    case "BTNF":
                    case "FNTB":
                        // We’ll keep raw FNT for optional name parsing; many NARCs don’t actually provide names.
                        fntRaw = br.ReadBytes((int)(blockSize - 8));
                        break;
                    
                    case "GMIF":
                    case "FIMG":
                        // This is the concatenated file data block.
                        narc._fileDataBaseOffset = input.Position;
                        narc._fileData = br.ReadBytes((int)(blockSize - 8));
                        break;
                    
                    default:
                        // Unknown block; skip safely
                        br.BaseStream.Seek(blockSize - 8, SeekOrigin.Current);
                        break;
                }
                
                // Ensure we’re at the end of the block even if something read fewer bytes.
                long expectedEnd = blockStart + blockSize;
                if (input.Position != expectedEnd)
                {
                    input.Seek(expectedEnd, SeekOrigin.Begin);
                }
            }

            if (fatEntries.Count == 0)
                throw new InvalidDataException("NARC missing FAT (BTAF/FATB) block.");
            if (narc._fileData == null || narc._fileData.Length == 0)
                throw new InvalidDataException("NARC missing file data (FIMG/GMIF) block or it is empty.");
            
            // Try to parse a simple flat name list from FNT if present (many files won’t have names).
            List<string>? names = null;
            if (fntRaw is not null && fntRaw.Length > 0)
            {
                names = TryParseSimpleFntNames(fntRaw);
            }
            narc.Names = names;
            
            // Build entries
            for (int i = 0; i < fatEntries.Count; i++)
            {
                var (start, end) = fatEntries[i];

                if (end < start || end > narc._fileData.Length)
                    throw new InvalidDataException($"FAT entry {i} has invalid range [{start}, {end}).");

                string? name = names != null && i < names.Count ? names[i] : null;
                int offset = (int)start;
                int length = (int)(end - start);
                // Peek FourCC
                string header = "    ";
                if (length >= 4)
                    header = Encoding.ASCII.GetString(narc._fileData, offset, 4);

                narc._entries.Add(NarcEntry.ParseNarcEntry(header, i, name, offset, length, narc._fileData));
            }
            
            return narc;
        }


        private static void ParseFat(BinaryReader br, long blockStart, uint blockSize,
            List<(uint start, uint end)> fatEntries)
        {
            // FAT structure:
            // u32 fileCount
            // fileCount times:
            //   u32 startOffset
            //   u32 endOffset
            // Offsets are relative to the start of FIMG/GMIF payload (not file start).
            if (blockSize < 12) // 8 header + at least fileCount(4)
                throw new InvalidDataException("FAT block too small.");

            uint fileCount = br.ReadUInt32();
            long expectedEntriesBytes = fileCount * 8L;
            long remaining = blockSize - 8 - 4; // subtract header(8) and fileCount(4)

            if (remaining < expectedEntriesBytes)
                throw new InvalidDataException($"FAT block too small for {fileCount} entries.");

            fatEntries.Capacity = (int)fileCount;
            for (uint i = 0; i < fileCount; i++)
            {
                uint start = br.ReadUInt32();
                uint end = br.ReadUInt32();
                fatEntries.Add((start, end));
            }

            // Skip any padding if present
            long consumed = 4 + expectedEntriesBytes;
            long padding = (blockSize - 8) - consumed;
            if (padding > 0)
                br.BaseStream.Seek(padding, SeekOrigin.Current);
        }
        
        // NOTE: Full FNT parsing (folders, subtrees) is somewhat involved.
        // This helper attempts a very simple “flat names” extraction for the common case.
        // If it fails, we return null and rely on numeric indices instead.
        private static List<string>? TryParseSimpleFntNames(byte[] fntBytes)
        {
            try
            {
                using var ms = new MemoryStream(fntBytes, writable: false);
                using var br = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

                // A common FNT layout starts with a u32 for table size, then directory entries.
                // However, in many game NARCs this isn’t populated meaningfully.
                // We’ll scan for simple zero-terminated ASCII segments as a best-effort fallback.
                var names = new List<string>();
                var current = new List<byte>();

                while (ms.Position < ms.Length)
                {
                    byte b = br.ReadByte();
                    if (b == 0)
                    {
                        if (current.Count > 0)
                        {
                            string s = Encoding.ASCII.GetString(current.ToArray());
                            // Filter out non-printables or trivial entries
                            if (!string.IsNullOrWhiteSpace(s) && s.All(c => c >= 32 && c < 127))
                                names.Add(s);
                            current.Clear();
                        }
                    }
                    else
                    {
                        current.Add(b);
                    }
                }

                // If we didn’t find anything reasonable, return null
                if (names.Count == 0)
                    return null;

                return names;
            }
            catch
            {
                return null;
            }
        }
        
        
        public static string ReadFourCC(BinaryReader br)
        {
            var bytes = br.ReadBytes(4);
            if (bytes.Length != 4) throw new EndOfStreamException("Unexpected end of stream reading FourCC.");
            return Encoding.ASCII.GetString(bytes);
        }
        
        // Common Nitro header: magic (4), BOM(2), ver(2), fileSize(4), hdrSize(2), blkCount(2)
        public struct NitroHeader
        {
            public string Magic;
            public ushort Bom;
            public ushort Version;
            public uint FileSize;
            public ushort HeaderSize;
            public ushort BlockCount;
        }

        public static NitroHeader ReadNitroHeader(BinaryReader br)
        {
            return new NitroHeader
            {
                Magic = ReadFourCC(br),
                Bom = br.ReadUInt16(),
                Version = br.ReadUInt16(),
                FileSize = br.ReadUInt32(),
                HeaderSize = br.ReadUInt16(),
                BlockCount = br.ReadUInt16()
            };
        }

        public static Color32 Bgr555ToColor32(ushort c, bool zeroIsTransparent = false, int paletteIndex = -1, int colorIndex = -1)
        {
            int r = (c & 0x1F);
            int g = (c >> 5) & 0x1F;
            int b = (c >> 10) & 0x1F;

            byte R = (byte)((r * 255 + 15) / 31);
            byte G = (byte)((g * 255 + 15) / 31);
            byte B = (byte)((b * 255 + 15) / 31);

            // By convention, color 0 of palette 0 is often transparent in DS assets.
            byte A = 255;
            if (zeroIsTransparent && paletteIndex == 0 && colorIndex == 0)
                A = 0;

            return new Color32(R, G, B, A);
        }
        
    }
}