using System.IO;
using WPUI.Nitro.Attributes;

namespace WPUI.Nitro.Files
{
    public sealed class CompressFile : NitroFile
    {
        public override bool HasChildFiles => true;
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