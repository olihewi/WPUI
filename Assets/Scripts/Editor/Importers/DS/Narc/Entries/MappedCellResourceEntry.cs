using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

namespace Pokemon.Importers.DS.Narc.Entries
{
    public class MappedCellResourceEntry : NarcEntry
    {
        private static HashSet<string> Magic = new(){ "NMCR", "RCMN" };

        public struct MappedAnimation
        {
            public MappedAnimationCell[] AnimationCells;
        }
        public struct MappedAnimationCell
        {
            public ushort AnimationCellIndex;
            public short X, Y;
            public ushort Priority;
        }

        public MappedAnimation[] Animations;
        
        public MappedCellResourceEntry(int index, [CanBeNull] string name, int offset, int length, byte[] fileData) : base(index, name, offset, length, fileData)
        {
            using var ms = new MemoryStream(fileData, Offset, Length, writable: false);
            using var br = new BinaryReader(ms, System.Text.Encoding.ASCII, leaveOpen: true);
            
            var hdr = NarcFile.ReadNitroHeader(br);
            if (!Magic.Contains(hdr.Magic))
                throw new InvalidDataException($"Not a NMCR entry: {hdr.Magic}");
            
            for (int b = 0; b < hdr.BlockCount; b++)
            {
                string fourCC = NarcFile.ReadFourCC(br);
                uint blkSize = br.ReadUInt32();
                long blkStart = ms.Position;

                if (fourCC == "KBCM")
                {
                    var AbnkLength = (int)(blkSize - 8);

                    byte[] payload = br.ReadBytes(AbnkLength);
                    try
                    {
                        ParseMcbk(payload);
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

        private void ParseMcbk(byte[] b)
        {
            var count = BitConverter.ToUInt16(b, 0x00);
            var headerOffset = BitConverter.ToUInt32(b, 0x04);
            var dataOffset = BitConverter.ToUInt32(b, 0x08);
            Animations = new MappedAnimation[count];
            int cellsCount = 0;
            for (uint headerIdx = 0; headerIdx < count; headerIdx++)
            {
                int q = (int)(headerOffset + headerIdx * 8);
                var cellCount = BitConverter.ToUInt16(b, q);
                var cellOffset = BitConverter.ToUInt32(b, q + 0x04);
                var animationCells = new MappedAnimationCell[cellCount];
                for (int cellIdx = 0; cellIdx < cellCount; cellIdx++)
                {
                    int v = (int)(dataOffset + cellOffset + cellIdx * 8);
                    var animationCell = new MappedAnimationCell()
                    {
                        AnimationCellIndex = BitConverter.ToUInt16(b, v),
                        X = BitConverter.ToInt16(b, v + 0x02),
                        Y = BitConverter.ToInt16(b, v + 0x04),
                        Priority = (byte)(BitConverter.ToInt16(b, v + 0x06) >> 8),
                    };
                    animationCells[cellIdx] = animationCell;
                }
                Animations[headerIdx] = new MappedAnimation() { AnimationCells = animationCells };
            }


        }
    }
}