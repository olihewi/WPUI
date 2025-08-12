using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using WPUI.Nitro.Attributes;
using WPUI.Nitro.Exceptions;

namespace WPUI.Nitro.Files
{
    [RequiredAttribute(typeof(MagicAttribute))]
    public abstract class NitroFile
    {
        public virtual bool HasChildFiles => false;
        public NitroFile[] ChildFiles;
        
        public MagicAttribute Magic
        {
            get
            {
                var attribs = GetType().GetCustomAttributes(typeof(MagicAttribute), false);
                return attribs.Length > 0 ? attribs[0] as MagicAttribute : null;
            }
        }
        
        
        [StructLayout(LayoutKind.Explicit)]
        public struct Header
        {
            [FieldOffset(0x00)] public uint Magic; // NARC
            [FieldOffset(0x04)] public ushort ByteOrder; // Typically 0xFFFE (Little Endian)
            [FieldOffset(0x06)] public ushort Version; // e.g. 0x0100
            [FieldOffset(0x08)] public uint FileSize; // Total file size per header
            [FieldOffset(0x0C)] public ushort HeaderSize; // Usually 0x0010
            [FieldOffset(0x0E)] public ushort BlockCount; // Typically 3 (FAT/FNT/FIMG)
        }
        public Header header;

        public abstract void Read(BinaryReader br);
        public abstract void Write(BinaryWriter bw);

        public void ReadHeader(BinaryReader br)
        {
            header = ReadStruct<Header>(br);
            if (Magic.Bytes != header.Magic) throw new InvalidMagicException(Magic.Bytes, header.Magic);
        }
        
        public static NitroFile ParseFile(BinaryReader br)
        {
            var magic = br.ReadUInt32();
            br.BaseStream.Seek(br.BaseStream.Position - 4, SeekOrigin.Current);
            if (!MagicAttribute.MagicTypeLookup.TryGetValue(magic, out var type) ||
                !type.IsAssignableFrom(typeof(NitroFile)))
            {
                type = typeof(NitroFile);
            }

            var file = (NitroFile)Activator.CreateInstance(type);
            file.Read(br);
            return file;

        }
        
        public static T ReadStruct<T>(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf<T>());
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                handle.Free();
            }
        }

        public static T[] ReadStructArray<T>(BinaryReader reader, uint count)
        {
            int size = Marshal.SizeOf<T>();
            T[] arr = new T[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = ReadStruct<T>(reader);
            }
            return arr;
        }

        public static string ReadMagic(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}