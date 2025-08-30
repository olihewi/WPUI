using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WPUI.Nitro.Attributes;

namespace WPUI.Nitro.Files
{
    [Magic("NMCR")]
    public sealed class AnimatedCharacterFile : NitroFile
    {
        [Magic("MCBK"), StructLayout(LayoutKind.Explicit, Size = 0x10)]
        public struct MCBKHeader
        {
            [FieldOffset(0x00)] public ushort AnimationCount;
            [FieldOffset(0x02)] public ushort _padding;
            [FieldOffset(0x04)] public uint MappedAnimationDataOffset;
            [FieldOffset(0x08)] public uint MappedAnimationCellDataOffset;
            [FieldOffset(0x0C)] public ulong _padding2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x08)]
        public struct MappedAnimation
        {
            [FieldOffset(0x00)] public ushort CellCount;
            [FieldOffset(0x02)] public ushort _unknown;
            [FieldOffset(0x04)] public uint MappedAnimationCellDataOffset;
        }


        [StructLayout(LayoutKind.Explicit, Size = 0x08)]
        public struct MappedAnimationCell
        {
            [FieldOffset(0x00)] public ushort AnimationCellIndex;
            [FieldOffset(0x02)] public short X;
            [FieldOffset(0x04)] public short Y;
            [FieldOffset(0x06)] public byte _unknown;
            [FieldOffset(0x07)] public byte Priority;
        }

        public MCBKHeader McbkHeader;
        public MappedAnimation[] MappedAnimations;
        public Dictionary<MappedAnimation, MappedAnimationCell[]> MappedAnimationCellsMap;
        
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
                    case "KBCM":
                    {
                        McbkHeader = ReadStruct<MCBKHeader>(br);
                        br.BaseStream.Seek(blockStart + 8 + McbkHeader.MappedAnimationDataOffset, SeekOrigin.Current);
                        MappedAnimations = ReadStructArray<MappedAnimation>(br, McbkHeader.AnimationCount);
                        MappedAnimationCellsMap =
                            new Dictionary<MappedAnimation, MappedAnimationCell[]>(McbkHeader.AnimationCount);
                        for (int animationIdx = 0; animationIdx < McbkHeader.AnimationCount; animationIdx++)
                        {
                            br.BaseStream.Seek(blockStart + 8 + McbkHeader.MappedAnimationCellDataOffset + MappedAnimations[animationIdx].MappedAnimationCellDataOffset, SeekOrigin.Current);
                            MappedAnimationCellsMap[MappedAnimations[animationIdx]] = ReadStructArray<MappedAnimationCell>(br, MappedAnimations[animationIdx].CellCount);
                        }
                        break;
                    }
                }
                br.BaseStream.Seek(blockStart + blockSize, SeekOrigin.Current);
            }
        }

        public override void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }
    }
}