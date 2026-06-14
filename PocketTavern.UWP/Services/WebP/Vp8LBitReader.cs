using System;

namespace PocketTavern.UWP.Services.WebP
{
    // LSB-first bit reader matching libwebp's VP8L bit reader semantics.
    // Bits are consumed least-significant-first from a 64-bit accumulator that
    // is refilled one byte at a time from the source buffer.
    internal sealed class Vp8LBitReader
    {
        private readonly byte[] _buf;
        private readonly int _end;   // exclusive end index in _buf
        private int _pos;            // next byte to load into the accumulator
        private ulong _val;          // accumulator; LSB == next bit to consume
        private int _bits;           // number of valid bits currently in _val

        // Set once the reader is asked for more bits than the stream contains.
        public bool Eos;

        public Vp8LBitReader(byte[] buf, int offset, int length)
        {
            _buf = buf;
            _pos = offset;
            _end = offset + length;
            Fill();
        }

        private void Fill()
        {
            // Keep at least 56 bits buffered so any read up to 24 bits succeeds.
            while (_bits <= 56 && _pos < _end)
            {
                _val |= (ulong)_buf[_pos++] << _bits;
                _bits += 8;
            }
        }

        // Reads n bits (0..24) LSB-first. Past end-of-stream returns whatever
        // bits remain, zero-padded, and latches Eos.
        public uint ReadBits(int n)
        {
            if (n == 0) return 0;
            if (_bits < n) Fill();
            if (_bits < n)
            {
                Eos = true;
                uint partial = (uint)(_val & ((_bits >= 32) ? 0xFFFFFFFFUL : ((1UL << _bits) - 1)));
                _val = 0;
                _bits = 0;
                return partial;
            }
            uint res = (uint)(_val & ((n >= 32) ? 0xFFFFFFFFUL : ((1UL << n) - 1)));
            _val >>= n;
            _bits -= n;
            return res;
        }

        // Reads a single bit LSB-first.
        public int ReadBit()
        {
            if (_bits < 1) Fill();
            if (_bits < 1)
            {
                Eos = true;
                return 0;
            }
            int b = (int)(_val & 1UL);
            _val >>= 1;
            _bits--;
            return b;
        }
    }
}
