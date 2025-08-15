using System.IO;
using System.Runtime.InteropServices;

namespace WPUI.Nitro.Structs
{
    public enum OamShape : byte
    {
        Square = 0,
        Horizontal = 1,
        Vertical = 2,
    }

    public enum OamMode : byte
    {
        Normal = 0,
        SemiTransparent = 1,
        ObjWindow = 2,
    }
    
    public struct Oam
    {
        // Packed Attributes
        [FieldOffset(0x00)] public ushort Attribute0;
        [FieldOffset(0x02)] public ushort Attribute1;
        [FieldOffset(0x04)] public ushort Attribute2;
        [FieldOffset(0x06)] public ushort? _padding;
        
        // Decoded from 0: SS D M OO P R YYYYYYYY
        public OamShape Shape
        {
            get => (OamShape)((Attribute0 >> 14) & 0x03);
            set => Attribute0 = (ushort)((Attribute0 & ~(0b11 << 14)) | ((ushort)value << 14));
        }
        public bool Use8BitsPerPixel
        {
            get => ((Attribute0 >> 13) & 0x01) != 0;
            set => Attribute0 = (ushort)((Attribute0 & ~(1 << 13)) | ((value ? 1 : 0) << 13));
        }
        public bool IsMosaic
        {
            get => ((Attribute0 >> 12) & 0x01) != 0;
            set => Attribute0 = (ushort)((Attribute0 & ~(1 << 12)) | ((value ? 1 : 0) << 12));
        }
        public OamMode Mode
        {
            get => (OamMode)((Attribute0 >> 10) & 0x03);
            set => Attribute0 = (ushort)((Attribute0 & ~(0b11 << 10)) | ((ushort)value << 10));
        }
        public bool HiddenOrDoubleSize // Dependent on IsRotationScale
        {
            get => ((Attribute0 >> 9) & 0x01) != 0;
            set => Attribute0 = (ushort)((Attribute0 & ~(1 << 9)) | ((value ? 1 : 0) << 9));
        }
        public bool IsRotationScale
        {
            get => ((Attribute0 >> 8) & 0x01) != 0;
            set => Attribute0 = (ushort)((Attribute0 & ~(1 << 8)) | ((value ? 1 : 0) << 8));
        }
        public short Y
        {
            get
            {
                var rawY = Attribute0 & 0x00FFu;
                return (short)((rawY >= 128) ? rawY - 256 : rawY);
            }
            set
            {
                ushort encoded = (ushort)(value < 0 ? value + 256 : value);
                Attribute0 = (ushort)((Attribute0 & ~0x00FF) | (encoded & 0x00FF));
            }
        }

        // Decoded from 1: SS PPPPP XXXXXXXXX
        public byte SizeIndex
        {
            get => (byte)((Attribute1 >> 14) & 0x03);
            set => Attribute1 = (ushort)((Attribute1 & ~(0b11 << 14)) | ((ushort)(value & 0x03) << 14));
        }
        public short RotationParamsOrFlipFlags // Dependent on IsRotationScale
        {
            get => (short)((Attribute1 >> 9) & 0x0F);
            set => Attribute1 = (ushort)((Attribute1 & ~(0x0F << 9)) | ((ushort)(value & 0x0F) << 9));
        }
        public short X
        {
            get
            {
                uint rawX = Attribute1 & 0x01FFu;
                return (short)((rawX >= 256) ? rawX - 512 : rawX);
            }
            set
            {
                ushort encoded = (ushort)(value < 0 ? value + 512 : value);
                Attribute1 = (ushort)((Attribute1 & ~0x01FF) | (encoded & 0x01FF));
            }
        }
        
        // Decoded from 2: IIII PP TTTTTTTTTT
        public byte PaletteID
        {
            get => (byte)((Attribute2 >> 12) & 0x07);
            set => Attribute2 = (ushort)((Attribute2 & ~(0b111 << 12)) | ((ushort)(value & 0x07) << 12));
        }
        public byte Priority
        {
            get => (byte)((Attribute2 >> 10) & 0x03);
            set => Attribute2 = (ushort)((Attribute2 & ~(0b11 << 10)) | ((ushort)(value & 0x03) << 10));
        }
        public ushort TileIndex
        {
            get => (ushort)(Attribute2 & 0x03FF);
            set => Attribute2 = (ushort)((Attribute2 & ~0x03FF) | (value & 0x03FF));
        }
        
        // Helpers
        public ushort Width =>
            Shape switch
            {
                OamShape.Square =>     SizeIndex switch { 0 =>  8, 1 => 16, 2 => 32, 3 => 64, _ => 8 },
                OamShape.Horizontal => SizeIndex switch { 0 => 16, 1 => 32, 2 => 32, 3 => 64, _ => 16 },
                OamShape.Vertical =>   SizeIndex switch { 0 =>  8, 1 => 8, 2 => 16, 3 => 32, _ => 8 },
                _ => 8,
            };

        public ushort Height =>
            Shape switch
            {
                OamShape.Square =>     SizeIndex switch { 0 =>  8, 1 => 16, 2 => 32, 3 => 64, _ => 8 },
                OamShape.Horizontal => SizeIndex switch { 0 =>  8, 1 => 8, 2 => 16, 3 => 32, _ => 8 },
                OamShape.Vertical =>   SizeIndex switch { 0 => 16, 1 => 32, 2 => 32, 3 => 64, _ => 16 },
                _ => 8,
            };

        public static Oam ReadOam(BinaryReader br, bool has8ByteStride = false)
        {
            var oam = new Oam{ Attribute0 = br.ReadUInt16(), Attribute1 = br.ReadUInt16(), Attribute2 = br.ReadUInt16() };
            if (has8ByteStride) br.ReadUInt16();
            return oam;
        }
    }
}