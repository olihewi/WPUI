using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

namespace Pokemon.Importers.DS.Narc.Entries
{
    public class AnimationResourceNarcEntry : NarcEntry
    {
        private static HashSet<string> Magic = new(){ "NANR", "RNAN", "NMAR", "RAMN" };
        public struct AnimationCell
        {
            public ushort FrameType; // 0 = 4 bytes, 1 = 16 bytes, 2 = 8 bytes
            public ushort CellType; // 1 in NANR, 2 in NMAR
            public AnimationFrame[] Frames;
        }
        public struct AnimationFrame
        {
            public ushort FrameDuration; // In frames @ 60FPS
            
            public ushort CellIndex;
            public short Theta; // Angle in 65536 degrees
            public short X, Y;
            public int xMag, yMag; // 1.19.12 fp
        }

        public AnimationCell[] Cells;
        
        public AnimationResourceNarcEntry(int index, [CanBeNull] string name, int offset, int length, byte[] fileData) : base(index, name, offset, length, fileData)
        {
            using var ms = new MemoryStream(fileData, Offset, Length, writable: false);
            using var br = new BinaryReader(ms, System.Text.Encoding.ASCII, leaveOpen: true);

            var hdr = NarcFile.ReadNitroHeader(br);
            if (!Magic.Contains(hdr.Magic))
                throw new InvalidDataException($"Not an NANR/NMAR entry: {hdr.Magic}");
            
            for (int b = 0; b < hdr.BlockCount; b++)
            {
                string fourCC = NarcFile.ReadFourCC(br);
                uint blkSize = br.ReadUInt32();
                long blkStart = ms.Position;

                if (fourCC == "KNBA")
                {
                    var AbnkLength = (int)(blkSize - 8);

                    byte[] payload = br.ReadBytes(AbnkLength);
                    try
                    {
                        ParseAbnk(payload);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                else
                {
                    ms.Position = blkStart + blkSize - 8;
                }
            }
        }

        private void ParseAbnk(byte[] b)
        {
            var cellCount = BitConverter.ToUInt16(b, 0x00);
            Cells = new AnimationCell[cellCount];
            var frameCount = BitConverter.ToUInt16(b, 0x02);
            var cellOffset = BitConverter.ToUInt32(b, 0x04);
            var frameOffset = BitConverter.ToUInt32(b, 0x08);
            var frameDataOffset = BitConverter.ToUInt32(b, 0x0C);

            const uint cellStride = 0x10;
            const uint frameStride = 0x8;
            

            for (uint cellIdx = 0; cellIdx < cellCount; cellIdx++)
            {
                int q = (int)(cellOffset + cellIdx * cellStride);
                var cellFrameCount = BitConverter.ToUInt32(b, q);
                var cell = new AnimationCell()
                {
                    Frames = new AnimationFrame[cellFrameCount],
                    FrameType = BitConverter.ToUInt16(b, q + 0x04),
                    CellType = BitConverter.ToUInt16(b, q + 0x06),
                };
                var cellFrameOffset = BitConverter.ToUInt32(b, q + 0x0C);
                for (uint frameIdx = 0; frameIdx < cellFrameCount; frameIdx++)
                {
                    var w = (int)(frameOffset + cellFrameOffset + frameIdx * frameStride);
                    var v = (int)(BitConverter.ToUInt32(b, w) + frameDataOffset);
                    var frame = new AnimationFrame()
                    {
                        FrameDuration = BitConverter.ToUInt16(b, w+0x04),
                    };
                    switch (cell.FrameType)
                    {
                    case 1: // 16 bytes
                        frame.CellIndex = BitConverter.ToUInt16(b, v);
                        frame.Theta = BitConverter.ToInt16(b, v + 0x02);
                        frame.xMag = BitConverter.ToInt32(b, v + 0x04);
                        frame.yMag = BitConverter.ToInt32(b, v + 0x08);
                        frame.X = BitConverter.ToInt16(b, v + 0x0C);
                        frame.Y = BitConverter.ToInt16(b, v + 0x0E);
                        break;
                    case 2: // 8 bytes
                        frame.CellIndex = BitConverter.ToUInt16(b, v);
                        frame.X = BitConverter.ToInt16(b, v + 0x04);
                        frame.Y = BitConverter.ToInt16(b, v + 0x06);
                        break;
                    default:
                        frame.CellIndex = BitConverter.ToUInt16(b, v);
                        break;
                    }
                    cell.Frames[frameIdx] = frame;
                }
                Cells[cellIdx] = cell;
            }
        }
    }
}