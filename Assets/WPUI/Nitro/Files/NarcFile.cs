using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WPUI.Nitro.Attributes;
using WPUI.Nitro.Exceptions;

namespace WPUI.Nitro.Files
{
    [Magic("NARC")]
    public sealed class NarcFile : NitroFile
    {
        public override bool HasChildFiles => true;
        
    #region Blocks

        [Magic("FATB")]
        public struct FileAllocationTable
        {
            [FieldOffset(0x00)] public uint FileCount;
            [FieldOffset(0x04)] public FileEntry[] FileEntries;
            
            [StructLayout(LayoutKind.Explicit)]
            public struct FileEntry
            {
                // Offsets are relative to the start of FIMG/GMIF payload.
                [FieldOffset(0x00)] public uint StartOffset;
                [FieldOffset(0x04)] public uint EndOffset;
            }
        }

        [Magic("FNTB")]
        public struct FileNameTable
        {
            [FieldOffset(0x00)] public uint NameCount;
            public NameEntry[] NameEntries;

            [StructLayout(LayoutKind.Explicit)]
            public struct NameEntry
            {
                [FieldOffset(0x00)] public uint NameTableOffset;
                [FieldOffset(0x04)] public ushort ParentDirectoryID;
            }

            [StructLayout(LayoutKind.Explicit)]
            public struct NameTableEntry
            {
                [FieldOffset(0x00)] public byte Length; // If high bit is set (0x80), it's a directory
                [FieldOffset(0x01)] public char[] Name;
                // byte Terminator
            }
        }

        [Magic("FIMG")]
        public struct FileImageTable
        {
            [FieldOffset(0x00)] public byte[] RawData;
        }


    #endregion

        public override void Read(BinaryReader br)
        {
            ReadHeader(br);

            long fileCount = -1;
            FileAllocationTable.FileEntry[] fileEntries = null;
            long fileImageTablePosition = -1;

            for (int blockIdx = 0; blockIdx < header.BlockCount; blockIdx++)
            {
                long blockStart = br.BaseStream.Position;
                string magic = ReadMagic(br);
                int blockSize = (int)br.ReadUInt32();
                switch (magic)
                {
                    case "BTAF":
                    {
                        fileCount = br.ReadUInt32();
                        fileEntries = ReadStructArray<FileAllocationTable.FileEntry>(br, (uint)fileCount);
                        break;
                    }
                    /*case "BTNF":
                    {
                        uint nameCount = br.ReadUInt32();
                        fileNameTable = new FileNameTable()
                        {
                            NameCount = nameCount,
                            NameEntries = ReadStructArray<FileNameTable.NameEntry>(br, nameCount),
                        };
                        
                        break;
                    }*/
                    case "GMIF":
                    {
                        fileImageTablePosition = br.BaseStream.Position;
                        break;
                    }
                }
                br.BaseStream.Seek(blockStart + blockSize, SeekOrigin.Current);
            }

            if (fileCount <= 0)
                throw new InvalidDataException("NARC missing File Allocation Table (FATB)!");
            if (fileImageTablePosition < 0)
                throw new InvalidDataException("NARC missing File Image Table (FIMG)!");

            ChildFiles = new NitroFile[fileCount];
            for (int i = 0; i < fileCount; i++)
            {
                br.BaseStream.Seek(fileImageTablePosition + fileEntries[i].StartOffset, SeekOrigin.Current);
                ChildFiles[i] = ParseFile(br);
            }
        }

        public override void Write(BinaryWriter bw)
        {
            throw new NotImplementedException();
        }
    }
}