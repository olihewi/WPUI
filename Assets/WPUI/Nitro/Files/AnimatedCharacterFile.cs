using System.IO;
using WPUI.Nitro.Attributes;

namespace WPUI.Nitro.Files
{
    [Magic("NMCR")]
    public sealed class AnimatedCharacterFile : NitroFile
    {
        public override void Read(BinaryReader br)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }
    }
}