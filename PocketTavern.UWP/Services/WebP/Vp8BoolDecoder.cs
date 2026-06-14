namespace PocketTavern.UWP.Services.WebP
{
    // VP8 arithmetic (boolean) entropy decoder — the canonical RFC 6386 §7.3
    // formulation. Bit-identical to libwebp's optimized variant; kept in this
    // simpler form for clarity and ease of verification.
    internal sealed class Vp8BoolDecoder
    {
        private readonly byte[] _buf;
        private int _pos;
        private readonly int _end;
        private uint _range;   // [128, 255]
        private uint _value;
        private int _bitCount;

        public Vp8BoolDecoder(byte[] buf, int offset, int length)
        {
            _buf = buf;
            _pos = offset;
            _end = offset + length;
            uint b0 = NextByte();
            uint b1 = NextByte();
            _value = (b0 << 8) | b1;
            _range = 255;
            _bitCount = 0;
        }

        private uint NextByte() => _pos < _end ? _buf[_pos++] : 0u;

        public int GetBit(int prob)
        {
            uint split = 1u + (((_range - 1u) * (uint)prob) >> 8);
            uint bigSplit = split << 8;
            int ret;
            if (_value >= bigSplit) { ret = 1; _range -= split; _value -= bigSplit; }
            else { ret = 0; _range = split; }
            while (_range < 128)
            {
                _value <<= 1;
                _range <<= 1;
                if (++_bitCount == 8) { _bitCount = 0; _value |= NextByte(); }
            }
            return ret;
        }

        // n-bit literal, most-significant bit first (each bit at prob 128).
        public int GetValue(int bits)
        {
            int v = 0;
            while (bits-- > 0) v = (v << 1) | GetBit(0x80);
            return v;
        }

        public int GetSignedValue(int bits)
        {
            int v = GetValue(bits);
            return GetBit(0x80) != 0 ? -v : v;
        }

        // Attaches a sign (prob 128) to an already-decoded magnitude.
        public int GetSigned(int v) => GetBit(0x80) != 0 ? -v : v;
    }
}
