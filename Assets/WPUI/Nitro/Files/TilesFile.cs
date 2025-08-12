using System.IO;
using System.Runtime.InteropServices;
using WPUI.Nitro.Attributes;
using WPUI.Nitro.Utils;

namespace WPUI.Nitro.Files
{
    [Magic("NCGR")]
    public sealed class TilesFile : NitroFile
    {
        [Magic("CHAR"), StructLayout(LayoutKind.Explicit)]
        public struct CHARBlock
        {
            [FieldOffset(0x00)] public ushort Width;
            [FieldOffset(0x02)] public ushort Height;
            [FieldOffset(0x04)] public ColorFormat ColorFormat; // 3 = 4bpp, 4=8bpp
            [FieldOffset(0x08)] public MappingMode MappingMode;
            [FieldOffset(0x0C)] public GraphicType GraphicType; // 0 = horizontal, 1 = lineal
            [FieldOffset(0x10)] public uint GraphicsDataSize;
            [FieldOffset(0x14)] public uint GraphicsDataOffset;
        }

        [Magic("CPOS"), StructLayout(LayoutKind.Explicit)]
        public struct CPOSBlock
        {
            [FieldOffset(0x00)] public ushort X;
            [FieldOffset(0x02)] public ushort Y;
            [FieldOffset(0x04)] public ushort Width;
            [FieldOffset(0x06)] public ushort Height;
        }
        
        public enum MappingMode : uint
        {
            _2D = 0x000000,
            _1D32k = 0x000010,
            _1D64k = 0x100010,
            _1D128k = 0x200010,
            _1D256k = 0x300010,
        }

        public enum GraphicType : uint
        {
            Character = 0,
            Bitmap = 1,
        }

        public CHARBlock CharBlock;
        public CPOSBlock CposBlock;
        public byte[] GraphicsData;
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
                    case "RAHC":
                    {
                        CharBlock = ReadStruct<CHARBlock>(br);
                        br.BaseStream.Seek(blockStart + 8 + CharBlock.GraphicsDataOffset, SeekOrigin.Current);
                        GraphicsData = br.ReadBytes((int)CharBlock.GraphicsDataSize);
                        break;
                    }
                    case "SOPC":
                    {
                        CposBlock = ReadStruct<CPOSBlock>(br);
                        break;
                    }
                }
                br.BaseStream.Seek(blockStart + blockSize, SeekOrigin.Current);
            }
        }

        public override void Write(BinaryWriter bw)
        {
            //WriteHeader(bw);
        }
    }
}