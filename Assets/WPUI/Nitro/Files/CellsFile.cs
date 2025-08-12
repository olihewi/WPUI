using System.IO;
using WPUI.Nitro.Attributes;

namespace WPUI.Nitro.Files
{
    [Magic("NCER")]
    public sealed class CellsFile : NitroFile
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