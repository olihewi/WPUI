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

        public void WriteHeader(BinaryWriter bw) => WriteStruct(bw, header);
        
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
        
        public static T ReadStruct<T>(BinaryReader br)
        {
            byte[] bytes = br.ReadBytes(Marshal.SizeOf<T>());
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

        public static void WriteStruct<T>(BinaryWriter bw, T obj)
        {
            int size = Marshal.SizeOf<T>();
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(obj, ptr, false);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            bw.Write(arr);
        }

        public static T[] ReadStructArray<T>(BinaryReader br, uint count)
        {
            T[] arr = new T[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = ReadStruct<T>(br);
            }
            return arr;
        }

        public static void WriteStructArray<T>(BinaryWriter bw, T[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                WriteStruct(bw, arr[i]);
            }
        }

        public static string ReadMagic(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}