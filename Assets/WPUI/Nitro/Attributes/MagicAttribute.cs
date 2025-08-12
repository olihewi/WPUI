using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace WPUI.Nitro.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
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

        public static Dictionary<uint, Type> MagicTypeLookup
        {
            get
            {
                if (_magicTypeLookup != null) return _magicTypeLookup;
                _magicTypeLookup = new Dictionary<uint, Type>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        var magicAttributes = type.GetCustomAttributes(typeof(MagicAttribute), false);
                        foreach (var magicAttribute in magicAttributes)
                        {
                            _magicTypeLookup.Add(((MagicAttribute)magicAttribute).Bytes, type);
                        }
                    }
                }
                return _magicTypeLookup;
            }
        }
    }
}