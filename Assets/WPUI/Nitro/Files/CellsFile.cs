using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WPUI.Nitro.Attributes;
using WPUI.Nitro.Structs;
using WPUI.Nitro.Utils;

namespace WPUI.Nitro.Files
{
    [Magic("NCER")]
    public sealed class CellsFile : NitroFile
    {
        [Magic("CEBK"), StructLayout(LayoutKind.Explicit)]
        public struct CEBKHeader
        {
            [FieldOffset(0x00)] public ushort CellCount;
            [FieldOffset(0x02)] public CellBankAttributes CellBankAttributes;
            [FieldOffset(0x04)] public uint CellDataOffset;                      // Relative to start of CEBK
            [FieldOffset(0x08)] public TilesFile.MappingMode MappingMode;
            [FieldOffset(0x0C)] public uint VramTransferDataOffset;              // Relative to start of CEBK or 0
            [FieldOffset(0x10)] public uint StringBankDataOffset;                // Relative to start of CEBK or 0
            [FieldOffset(0x14)] public uint UserExtendedCellAttributeDataOffset; // Relative to start of CEBK or 0
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Cell
        {
            // Required
            [FieldOffset(0x00)] public ushort OamCount;
            [FieldOffset(0x02)] public ushort CellAttributes;
            [FieldOffset(0x04)] public uint OamDataOffset; // Relative to start of OAM data (after cell array)
            // Optional, determined by CEBKHeader.CellBankAttributes
            [FieldOffset(0x08)] public short xMax;
            [FieldOffset(0x0A)] public short yMax;
            [FieldOffset(0x0C)] public short xMin;
            [FieldOffset(0x0E)] public short yMin;
        }


        [Flags]
        public enum CellBankAttributes : ushort
        {
            None = 0,
            ContainsBoundingBoxes = 1,
        }

        public CEBKHeader CebkHeader;
        public Cell[] Cells;
        public Dictionary<Cell, Oam[]> CellOamsMap;
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
                    case "KBEC":
                    {
                        CebkHeader = ReadStruct<CEBKHeader>(br);
                        bool containsBoundingBoxes = (CebkHeader.CellBankAttributes & CellBankAttributes.ContainsBoundingBoxes) != 0;
                        var cellsBase = blockStart + 8 + CebkHeader.CellDataOffset;
                        var cellStride = containsBoundingBoxes ? 0x10 : 0x08;
                        var oamsBase = cellsBase + CebkHeader.CellCount * cellStride;
                        
                        Cells = new Cell[CebkHeader.CellCount];
                        CellOamsMap = new Dictionary<Cell, Oam[]>(CebkHeader.CellCount);
                        for (int cellIdx = 0; cellIdx < CebkHeader.CellCount; cellIdx++)
                        {
                            br.BaseStream.Seek(cellsBase + cellIdx * cellStride, SeekOrigin.Current);
                            var cell = new Cell()
                            {
                                OamCount = br.ReadUInt16(),
                                CellAttributes = br.ReadUInt16(),
                                OamDataOffset = br.ReadUInt32(),
                            };
                            if (containsBoundingBoxes)
                            {
                                cell.xMax = br.ReadInt16();
                                cell.yMax = br.ReadInt16();
                                cell.xMin = br.ReadInt16();
                                cell.yMin = br.ReadInt16();
                            }

                            br.BaseStream.Seek(oamsBase + cell.OamDataOffset, SeekOrigin.Current);
                            var oams = CellOamsMap[cell] = new Oam[cell.OamCount];
                            for (int oamIdx = 0; oamIdx < cell.OamCount; oamIdx++)
                            {
                                oams[oamIdx] = Oam.ReadOam(br);
                            }
                        }
                        break;
                    }
                    // TODO LABL Block
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