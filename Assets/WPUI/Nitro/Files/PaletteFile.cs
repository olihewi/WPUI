using System.IO;
using System.Runtime.InteropServices;
using WPUI.Nitro.Attributes;
using WPUI.Nitro.Utils;

namespace WPUI.Nitro.Files
{
    [Magic("NCLR")]
    public sealed class PaletteFile : NitroFile
    {
        [Magic("PLTT"), StructLayout(LayoutKind.Explicit)]
        public struct PLTTHeader
        {
            [FieldOffset(0x00)] public ColorFormat ColorFormat; // 3 = 4bpp, 4=8bpp
            [FieldOffset(0x04)] public uint ExtendedPalette;
            [FieldOffset(0x08)] public uint PaletteDataSize;
            [FieldOffset(0x0C)] public uint PaletteDataOffset; // Offset from TTLP+8 to Palette Data
        }

        public PLTTHeader PlttHeader;
        public Rgb8Color[] colors;
        public override void Read(BinaryReader br)
        {
            ReadHeader(br);
            for (int blockIdx = 0; blockIdx < header.BlockCount; blockIdx++)
            {
                long blockStart = br.BaseStream.Position;
                string magic = ReadMagic(br);
                int blockSize = (int)br.ReadUInt32();
                switch (magic)
                {
                    case "TTLP":
                    {
                        PlttHeader = ReadStruct<PLTTHeader>(br);
                        br.BaseStream.Seek(blockStart + 8 + PlttHeader.PaletteDataOffset, SeekOrigin.Current);
                        colors = new Rgb8Color[PlttHeader.PaletteDataSize / sizeof(uint)];
                        for (int colorIdx = 0; colorIdx < colors.Length; colorIdx++)
                        {
                            colors[colorIdx] = ColorUtils.ReadRgb8FromBgr555(br);
                        }
                        break;
                    }
                    // TODO PCMP block for compressed colors
                }
                br.BaseStream.Seek(blockStart + blockSize, SeekOrigin.Current);
            }
        }

        public override void Write(BinaryWriter bw)
        {
            var plltSize = Marshal.SizeOf<PLTTHeader>();
            PlttHeader.PaletteDataSize = (uint)colors.Length * 2;
            PlttHeader.PaletteDataOffset = (uint)(bw.BaseStream.Position + plltSize);
            header.BlockCount = 1;
            header.FileSize = header.HeaderSize + 8u + (uint)plltSize + PlttHeader.PaletteDataSize;
            
            WriteHeader(bw);
            bw.Write(MagicAttribute.GetMagicBytes(typeof(PLTTHeader)));
            bw.Write(8U + (uint)plltSize + PlttHeader.PaletteDataSize);
            WriteStruct(bw, PlttHeader);
            for (int i = 0; i < colors.Length; i++)
            {
                bw.Write(ColorUtils.Rgb8ToBgr555(colors[i]));
            }
        }
    }
}