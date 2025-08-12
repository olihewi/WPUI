using System;
using System.Text;

namespace WPUI.Nitro.Exceptions
{
    public class InvalidMagicException : Exception
    {
        public InvalidMagicException(uint expectedMagic, uint magic)
            : base($"Invalid {Encoding.ASCII.GetString(BitConverter.GetBytes(expectedMagic))} magic: {Encoding.ASCII.GetString(BitConverter.GetBytes(magic))}") {}
    }
}