using System.IO;
using DSDecmp.Formats.Nitro;
using WPUI.Nitro.Attributes;

namespace WPUI.Nitro.Files
{
    public sealed class CompressFile : NitroFile
    {
        public static CompositeNDSFormat CompositeNdsFormat = new();
        public override bool HasChildFiles => true;
        public override void Read(BinaryReader br)
        {
        }

        public override void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }
    }
}