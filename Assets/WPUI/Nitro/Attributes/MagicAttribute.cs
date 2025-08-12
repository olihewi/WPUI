using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace WPUI.Nitro.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class MagicAttribute : Attribute
    {
        public MagicAttribute(string magic)
        {
            Magic = magic;
            Bytes = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(magic));
        }

        public MagicAttribute(uint bytes)
        {
            Bytes = bytes;
            Magic = Encoding.ASCII.GetString(BitConverter.GetBytes(bytes));
        }
        
        public string Magic;
        public uint Bytes;

        private static Dictionary<uint, Type> _magicTypeLookup;
        private static Dictionary<Type, uint> _typeMagicLookup;
        public static Dictionary<uint, Type> MagicTypeLookup
        {
            get
            {
                if (_magicTypeLookup != null) return _magicTypeLookup;
                InitializeLookup();
                return _magicTypeLookup;
            }
        }
        public static Dictionary<Type, uint> TypeMagicLookup
        {
            get
            {
                if (_typeMagicLookup != null) return _typeMagicLookup;
                InitializeLookup();
                return _typeMagicLookup;
            }
        }
        
        private static void InitializeLookup()
        {
            _magicTypeLookup = new Dictionary<uint, Type>();
            _typeMagicLookup = new Dictionary<Type, uint>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var magicAttributes = type.GetCustomAttributes(typeof(MagicAttribute), false);
                    foreach (var magicAttribute in magicAttributes)
                    {
                        _magicTypeLookup.Add(((MagicAttribute)magicAttribute).Bytes, type);
                        _typeMagicLookup.Add(type, ((MagicAttribute)magicAttribute).Bytes);
                    }
                }
            }
        }

        public static uint GetMagicBytes(Type type) => TypeMagicLookup.TryGetValue(type, out var val) ? val : 0;

    }
}