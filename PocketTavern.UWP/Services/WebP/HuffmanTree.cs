namespace PocketTavern.UWP.Services.WebP
{
    // Canonical (RFC-1951 style) Huffman decoder built from per-symbol code
    // lengths. VP8L transmits codes MSB-first (verified against libwebp's
    // bit-reversed table construction in huffman_utils.c), so a plain
    // most-significant-bit-first tree walk decodes them correctly.
    internal sealed class HuffmanTree
    {
        private const int MaxLen = 15;

        private readonly int[] _count = new int[MaxLen + 1];  // codes per length
        private int[] _symbols;                                // sorted by len then symbol
        private int _single = -1;                              // single-symbol tree: 0 bits

        // Builds a tree from the first 'size' code lengths. Returns null if the
        // lengths describe no usable code.
        public static HuffmanTree Build(int[] codeLengths, int size)
        {
            var t = new HuffmanTree();
            int used = 0;
            int lastSym = -1;
            for (int i = 0; i < size; i++)
            {
                int l = codeLengths[i];
                if (l < 0 || l > MaxLen) return null;
                if (l > 0) { t._count[l]++; used++; lastSym = i; }
            }
            if (used == 0) return null;
            if (used == 1)
            {
                // Single symbol consumes no bits, matching libwebp's bits==0 entry.
                t._single = lastSym;
                return t;
            }

            var offset = new int[MaxLen + 2];
            offset[1] = 0;
            for (int l = 1; l < MaxLen; l++)
            {
                if (t._count[l] > (1 << l)) return null;  // over-subscribed
                offset[l + 1] = offset[l] + t._count[l];
            }

            var sorted = new int[used];
            for (int i = 0; i < size; i++)
            {
                int l = codeLengths[i];
                if (l > 0) sorted[offset[l]++] = i;
            }
            t._symbols = sorted;
            return t;
        }

        public int ReadSymbol(Vp8LBitReader br)
        {
            if (_single >= 0) return _single;

            int code = 0, first = 0, index = 0;
            for (int len = 1; len <= MaxLen; len++)
            {
                code |= br.ReadBit();
                int cnt = _count[len];
                if (code - first < cnt) return _symbols[index + (code - first)];
                index += cnt;
                first = (first + cnt) << 1;
                code <<= 1;
            }
            return 0;  // malformed stream; caller checks br.Eos
        }
    }
}
