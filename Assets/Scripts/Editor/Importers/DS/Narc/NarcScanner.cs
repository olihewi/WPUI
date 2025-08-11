using System;
using System.Collections.Generic;
using System.Text;

namespace Pokemon.Importers.DS.Narc
{
    public struct NitroHit
    {
        public string Magic;
        public int Offset;
        public int Length;   // Includes whole Nitro file (from magic to end)
    }

    public static class NitroScanner
    {
        public static List<NitroHit> FindSubfiles(byte[] buf, int start, int length)
        {
            var hits = new List<NitroHit>();
            int end = start + length;

            for (int i = start; i + 16 <= end; i++)
            {
                string magic = TryFourCC(buf, i);
                if (magic == null) continue;


                // Nitro header: magic(4) + bom(2) + ver(2) + fileSize(4) + hdrSize(2) + blkCount(2)
                if (i + 12 > end) continue;
                uint fileSize = BitConverter.ToUInt32(buf, i + 8);
                if (fileSize == 0 || i + fileSize > end) continue;

                hits.Add(new NitroHit
                {
                    Magic = magic,
                    Offset = i,
                    Length = (int)fileSize,
                });

                // Skip ahead to end of this subfile to avoid redundant scans
                i += (int)fileSize - 1;
            }

            return hits;
        }

        private static string TryFourCC(byte[] buf, int offset)
        {
            byte b0 = buf[offset];
            if (b0 < 0x20 || b0 > 0x7E) return null;
            if (buf[offset + 1] < 0x20 || buf[offset + 1] > 0x7E) return null;
            if (buf[offset + 2] < 0x20 || buf[offset + 2] > 0x7E) return null;
            if (buf[offset + 3] < 0x20 || buf[offset + 3] > 0x7E) return null;
            return Encoding.ASCII.GetString(buf, offset, 4);
        }
    }
}