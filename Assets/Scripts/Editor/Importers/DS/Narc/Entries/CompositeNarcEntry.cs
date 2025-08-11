using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DSDecmp.Formats.Nitro;
using JetBrains.Annotations;
using UnityEngine;

namespace Pokemon.Importers.DS.Narc.Entries
{
    public class CompositeNarcEntry : NarcEntry
    {
        public IReadOnlyList<NarcEntry> Children => _children;
        private readonly List<NarcEntry> _children = new();

        public static CompositeNDSFormat compositeNds = new();
        public CompositeNarcEntry(int index, [CanBeNull] string name, int offset, int length, byte[] fileData) : base(index, name, offset, length, fileData)
        {
            // Step 1: try decompression
            byte[] slice = GetData();
            using var inStream = new MemoryStream(slice, 0, slice.Length);
            using var outStream = new MemoryStream(99999);
            using var writer = new BinaryWriter(outStream, Encoding.ASCII);
            var decompressed = slice;
            if (compositeNds.Supports(inStream, slice.Length))
            {
                inStream.Seek(0, SeekOrigin.Begin);
                try
                {
                    compositeNds.Decompress(inStream, slice.Length, outStream);
                    decompressed = outStream.ToArray();
                }
                catch (Exception e)
                {
                    /*
                    var hexBuilder = new StringBuilder(slice.Length * 2);

                    foreach (byte b in slice)
                    {
                        hexBuilder.AppendFormat("{0:X2}", b);
                    }

                    string hexString = hexBuilder.ToString();
                    Debug.Log(hexString);
                    Debug.LogException(e);*/
                }
            }
            //var decompressed = DsLz.TryDecompressAt(slice, 0, slice.Length) ?? slice;

            // Step 2: scan for Nitro subfiles
            var hits = NitroScanner.FindSubfiles(decompressed, 0, decompressed.Length);
            if (hits.Count == 0)
                return;

            // Step 3: promote hits into typed children
            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                // Create a child over the decompressed segment with offset 0
                byte[] sub = new byte[h.Length];
                Buffer.BlockCopy(decompressed, h.Offset, sub, 0, h.Length);

                var child = ParseNarcEntry(h.Magic, i, NameChild(i), 0, sub.Length, sub, true);
                _children.Add(child);
            }
        }
        private string NameChild(int i) =>
            (Name != null ? $"{Name}_{i:D2}" : $"{Index}_{i:D2}");
    }
}