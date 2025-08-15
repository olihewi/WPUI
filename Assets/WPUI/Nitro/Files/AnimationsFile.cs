using System;
using System.IO;
using System.Runtime.InteropServices;
using WPUI.Nitro.Attributes;

namespace WPUI.Nitro.Files
{
    [Magic("NANR")]
    public sealed class AnimationsFile : NitroFile
    {
        [Magic("ABNK"), StructLayout(LayoutKind.Explicit)]
        public struct ABNKHeader
        {
            [FieldOffset(0x00)] public ushort CellCount;
            [FieldOffset(0x02)] public ushort FrameCount;
            [FieldOffset(0x04)] public uint CellDataOffset;
            [FieldOffset(0x08)] public uint FrameDataOffset;
            [FieldOffset(0x0C)] public uint FrameTransformDataOffset;
            [FieldOffset(0x010)] public ulong _padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x10)]
        public struct AnimationCell
        {
            [FieldOffset(0x00)] public uint FrameCount;
            [FieldOffset(0x04)] public FrameType FrameType;
            [FieldOffset(0x06)] public CellType CellType;
            [FieldOffset(0x08)] public uint _unknown;
            [FieldOffset(0x0C)] public uint FrameDataOffset; // Relative to ABNKHeader.FrameDataOffset

            public Frame[] Frames;
        }

        public enum FrameType : ushort
        {
            CellOnly = 0,
            PositionAndRotation = 1,
            Position = 2,
        }

        public enum CellType : ushort
        {
            NANR = 1,
            NAMR = 2,
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x08)]
        public struct Frame
        {
            [FieldOffset(0x00)] public uint TransformDataOffset; // Relative to ABNKHeader.FrameTransformDataOffset
            [FieldOffset(0x04)] public ushort FrameDuration; // In frames @ 60FPS
            [FieldOffset(0x06)] public ushort _padding; // Always 0xBEEF

            public FrameTransform Transform;
        }

        public abstract class FrameTransform
        {
            
        }
        
        [StructLayout(LayoutKind.Explicit)]
        public class FrameTransformCellOnly : FrameTransform
        {
            [FieldOffset(0x00)] public ushort CellIndex;
            [FieldOffset(0x02)] public ushort _unknown;
        }

        [StructLayout(LayoutKind.Explicit)]
        public class FrameTransformPositionAndRotation : FrameTransform
        {
            [FieldOffset(0x00)] public ushort CellIndex;
            [FieldOffset(0x02)] public short Rotation; // Angle in 65536 degrees
            [FieldOffset(0x04)] public int XMagnitude;
            [FieldOffset(0x08)] public int YMagnitude;
            [FieldOffset(0x0C)] public short X;
            [FieldOffset(0x0E)] public short Y;
        }

        [StructLayout(LayoutKind.Explicit)]
        public class FrameTransformPosition : FrameTransform
        {
            [FieldOffset(0x00)] public ushort CellIndex;
            [FieldOffset(0x02)] public ushort _padding;
            [FieldOffset(0x04)] public short X;
            [FieldOffset(0x06)] public short Y;
        }

        public ABNKHeader AbnkHeader;
        public AnimationCell[] Cells;
        
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
                    case "KNBA":
                    {
                        AbnkHeader = ReadStruct<ABNKHeader>(br);
                        br.BaseStream.Seek(blockStart + 8 + AbnkHeader.CellDataOffset, SeekOrigin.Current);
                        Cells = ReadStructArray<AnimationCell>(br, AbnkHeader.CellCount);
                        for (int cellIdx = 0; cellIdx < AbnkHeader.CellCount; cellIdx++)
                        {
                            br.BaseStream.Seek(blockStart + 8 + AbnkHeader.FrameDataOffset + Cells[cellIdx].FrameDataOffset, SeekOrigin.Current);
                            Cells[cellIdx].Frames = ReadStructArray<Frame>(br, Cells[cellIdx].FrameCount);
                            
                            for (int frameIdx = 0; frameIdx < Cells[cellIdx].FrameCount; frameIdx++)
                            {
                                br.BaseStream.Seek(blockStart + 8 + AbnkHeader.FrameTransformDataOffset + Cells[cellIdx].Frames[frameIdx].TransformDataOffset, SeekOrigin.Current);
                                Cells[cellIdx].Frames[frameIdx].Transform = Cells[cellIdx].FrameType switch
                                {
                                    FrameType.CellOnly => ReadStruct<FrameTransformCellOnly>(br),
                                    FrameType.PositionAndRotation => ReadStruct<FrameTransformPositionAndRotation>(br),
                                    FrameType.Position => ReadStruct<FrameTransformPosition>(br),
                                    _ => throw new ArgumentOutOfRangeException()
                                };
                            }
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