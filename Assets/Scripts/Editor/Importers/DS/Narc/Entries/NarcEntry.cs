using System;
using System.IO;
using JetBrains.Annotations;

namespace Pokemon.Importers.DS.Narc.Entries
{
    public class NarcEntry
    {
        public int Index { get; }
        [CanBeNull] public string Name { get; }
        public int Offset { get; }
        public int Length { get; }

        private readonly byte[] _fileData;

        public NarcEntry(int index, [CanBeNull] string name, int offset, int length, byte[] fileData)
        {
            Index = index;
            Name = name;
            Offset = offset;
            Length = length;
            _fileData = fileData ?? throw new ArgumentNullException(nameof(fileData));
        }

        public byte[] GetData()
        {
            var slice = new byte[Length];
            Buffer.BlockCopy(_fileData, Offset, slice, 0, Length);
            return slice;
        }

        public Stream OpenStream()
        {
            // Return a non-copying stream over the slice.
            // Using MemoryStream(byte[], int, int, bool, bool) keeps it non-writable and non-exposable.
        #if UNITY_2021_2_OR_NEWER
            return new MemoryStream(_fileData, Offset, Length, writable: false, publiclyVisible: false);
        #else
            // Older runtimes may not support publiclyVisible; fallback is still fine.
            return new MemoryStream(_fileData, Offset, Length, writable: false);
        #endif
        }
        
        public override string ToString()
        {
            string type = GetType().ToString();
            string id = Name != null ? $"{Index}: {Name}" : $"{Index}";
            return $"{id} [{Offset}, {Offset + Length}] {type}";
        }

        public static NarcEntry ParseNarcEntry(string header, int i, string name, int offset, int length,
            byte[] fileData, bool isInComposite = false)
        {
            return header switch
            {
                "RLCN" => new PaletteNarcEntry(i, name, offset, length, fileData),
                "NCLR" => new PaletteNarcEntry(i, name, offset, length, fileData),
                "RGCN" => new TilesNarcEntry(i, name, offset, length, fileData),
                "NCGR" => new TilesNarcEntry(i, name, offset, length, fileData),
                "RECN" => new CellsNarcEntry(i, name, offset, length, fileData),
                "NCER" => new CellsNarcEntry(i, name, offset, length, fileData),
                "NANR" => new AnimationResourceNarcEntry(i, name, offset, length, fileData),
                "RNAN" => new AnimationResourceNarcEntry(i, name, offset, length, fileData),
                "NMCR" => new MappedCellResourceEntry(i, name, offset, length, fileData),
                "RCMN" => new MappedCellResourceEntry(i, name, offset, length, fileData),
                _ => isInComposite 
                    ? new NarcEntry(i, name, offset, length, fileData)
                    : ParseCompositeOrDefaultNarcEntry(i, name, offset, length, fileData),
            };
        }
        private static NarcEntry ParseCompositeOrDefaultNarcEntry(int i, [CanBeNull] string name, int offset, int length, byte[] fileData)
        {
            var comp = new CompositeNarcEntry(i, name, offset, length, fileData);
            return (comp.Children?.Count ?? 0) > 0 ? comp : new NarcEntry(i, name, offset, length, fileData);
        }
    }
}