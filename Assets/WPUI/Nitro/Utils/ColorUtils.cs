using System.IO;

namespace WPUI.Nitro.Utils
{
    public static class ColorUtils
    {
        public static Rgb8Color ReadRgb8FromBgr555(BinaryReader br) => Bgr555ToRgb8(br.ReadUInt16());
        public static Rgb8Color Bgr555ToRgb8(ushort bgr)
        {
            int r = (bgr & 0x1F);
            int g = (bgr >> 5) & 0x1F;
            int b = (bgr >> 10) & 0x1F;

            byte R = (byte)((r * 255 + 15) / 31);
            byte G = (byte)((g * 255 + 15) / 31);
            byte B = (byte)((b * 255 + 15) / 31);

            return new Rgb8Color {R = R, G = G, B = B};
        }

        public static ushort Rgb8ToBgr555(Rgb8Color color)
        {
            // Convert 8-bit channels to 5-bit
            ushort R = (ushort)(color.R >> 3); // 0–31
            ushort G = (ushort)(color.G >> 3);
            ushort B = (ushort)(color.B >> 3);

            // Pack into BGR555 format: [0|RRRRR|GGGGG|BBBBB]
            return (ushort)((R << 10) | (G << 5) | B);
        }
    }

    public struct Rgb8Color
    {
        public byte R, G, B;
    }

    public enum ColorFormat : uint
    {
        _4BitsPerPixel = 3,
        _8BitsPerPixel = 4,
    }
}