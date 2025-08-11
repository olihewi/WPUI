using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Pokemon.Importers.DS
{
    public static class DsLz
    {
        public struct LzHeader
        {
            public byte Type;      // 0x10 or 0x11
            public int OutSize;    // decompressed size
            public int PayloadOfs; // offset where tokens begin (relative to input array start)
        }

        // Try to parse an LZ header at 'offset'. Returns true if valid.
        public static bool TryReadHeader(byte[] data, int offset, int length, out LzHeader hdr)
        {
            hdr = default;
            if (length < 4) return false;

            byte type = data[offset];
            if (type != 0x10 && type != 0x11) return false;

            // 24-bit decompressed size; if zero, next 4 bytes contain size
            int size24 = data[offset + 1] | (data[offset + 2] << 8) | (data[offset + 3] << 16);
            int payloadOfs = offset + 4;
            int outSize;

            if (size24 == 0)
            {
                if (length < 8) return false;
                outSize = BitConverter.ToInt32(data, offset + 4);
                payloadOfs = offset + 8;
            }
            else
            {
                outSize = size24;
            }

            if (outSize <= 0) return false;

            hdr = new LzHeader { Type = type, OutSize = outSize, PayloadOfs = payloadOfs };
            return true;
        }

        // Attempts to decompress from 'offset'; if header not valid, returns null.
        public static byte[]? TryDecompressAt(byte[] data, int offset, int length)
        {
            try
            {
                if (!TryReadHeader(data, offset, length, out var hdr)) return null;
                return hdr.Type == 0x10
                    ? DecompressLz10(data, hdr.PayloadOfs, offset + length - hdr.PayloadOfs, hdr.OutSize)
                    : DecompressLz11(data, hdr.PayloadOfs, offset + length - hdr.PayloadOfs, hdr.OutSize);
            }
            catch(Exception e)
            {
                StringBuilder hex = new StringBuilder(data.Length * 2);
                foreach (byte b in data)
                    hex.AppendFormat("{0:x2}", b);
                string hexString = hex.ToString();
                Debug.Log(hexString);
                Debug.LogException(e);
                return null;
            }
        }

        private static byte[] DecompressLz10(byte[] src, int si, int inRemain, int outSize)
        {
            var dst = new byte[outSize];
            int end = si + inRemain;
            int di = 0;

            while (di < outSize)
            {
                if (si >= end) Throw("LZ10: ran out of input", si, di);
                int flags = src[si++];

                for (int bit = 0; bit < 8 && di < outSize; bit++)
                {
                    bool isCompressed = (flags & (0x80 >> bit)) != 0;

                    if (!isCompressed)
                    {
                        if (si >= end) Throw("LZ10: expected literal", si, di);
                        dst[di++] = src[si++];
                        continue;
                    }

                    if (si + 1 >= end) Throw("LZ10: expected 2-byte backref", si, di);
                    int b1 = src[si++], b2 = src[si++];
                    int length = (b1 >> 4) + 3;
                    int disp = ((b1 & 0x0F) << 8) | b2;

                    int copyFrom = di - (disp + 1);
                    if (copyFrom < 0) Throw($"LZ10: backref before start (disp={disp}, di={di})", si, di);

                    for (int k = 0; k < length && di < outSize; k++)
                        dst[di++] = dst[copyFrom + k];
                }
            }

            return dst;
        }

        private static byte[] DecompressLz11(byte[] src, int si, int inRemain, int outSize)
        {
            var dst = new byte[outSize];
            int end = si + inRemain;
            int di = 0;

            while (di < outSize)
            {
                if (si >= end) Throw("LZ11: ran out of input", si, di);
                int flags = src[si++];

                for (int bit = 0; bit < 8 && di < outSize; bit++)
                {
                    bool isCompressed = (flags & (0x80 >> bit)) != 0;

                    if (!isCompressed)
                    {
                        if (si >= end) Throw("LZ11: expected literal", si, di);
                        dst[di++] = src[si++];
                        continue;
                    }

                    if (si >= end) Throw("LZ11: expected token", si, di);
                    int b0 = src[si++];
                    int hi = b0 >> 4;

                    int length, disp;

                    if (hi == 0)
                    {
                        // length = (((b0&F)<<12) | (b1<<4) | (b2>>4)) + 0x11
                        // disp   = ((b2&F) << 8) | b3
                        if (si + 2 >= end) Throw("LZ11: need 3 bytes for len(0)", si, di);
                        int b1 = src[si++], b2 = src[si++];
                        length = ((b0 & 0x0F) << 12) | (b1 << 4) | (b2 >> 4);
                        length += 0x11;
                        if (si >= end) Throw("LZ11: need 1 byte for disp(0)", si, di);
                        int b3 = src[si++];
                        disp = ((b2 & 0x0F) << 8) | b3;
                    }
                    else if (hi == 1)
                    {
                        // length = (((b0&F)<<20) | (b1<<12) | (b2<<4) | (b3>>4)) + 0x111
                        // disp   = ((b3&F) << 8) | b4
                        if (si + 3 >= end) Throw("LZ11: need 4 bytes for len(1)", si, di);
                        int b1 = src[si++], b2 = src[si++], b3 = src[si++];
                        length = ((b0 & 0x0F) << 20) | (b1 << 12) | (b2 << 4) | (b3 >> 4);
                        length += 0x111;
                        if (si >= end) Throw("LZ11: need 1 byte for disp(1)", si, di);
                        int b4 = src[si++];
                        disp = ((b3 & 0x0F) << 8) | b4;
                    }
                    else
                    {
                        // length = hi + 1
                        // disp   = ((b0&F) << 8) | b1
                        if (si >= end) Throw("LZ11: need 1 byte for disp(2..F)", si, di);
                        int b1 = src[si++];
                        length = hi + 1;
                        disp = ((b0 & 0x0F) << 8) | b1;
                    }

                    int copyFrom = di - (disp + 1);
                    if (copyFrom < 0) Throw($"LZ11: backref before start (disp={disp}, di={di})", si, di);

                    for (int k = 0; k < length; k++)
                    {
                        if (di >= outSize) break;
                        dst[di++] = dst[copyFrom + k];
                    }
                }
            }

            return dst;
        }

        private static void Throw(string msg, int si, int di)
            => throw new InvalidDataException($"{msg} (src=0x{si:X}, dst={di})");
    }
}