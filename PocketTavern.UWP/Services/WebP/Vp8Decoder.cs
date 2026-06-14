using System;

namespace PocketTavern.UWP.Services.WebP
{
    // Pure-managed VP8 (lossy WebP) key-frame decoder. Port of libwebp 1.3.2
    // (vp8_dec.c, tree_dec.c, quant_dec.c, frame_dec.c, dsp/dec.c). Decodes the
    // intra-only key frame to YUV420 planes, applies the in-loop filter, then
    // converts to ARGB.
    internal sealed class Vp8Decoder
    {
        private const int BPS = Vp8Dsp.BPS;
        private const int YUV_SIZE = BPS * 17 + BPS * 9;
        private const int Y_OFF = BPS * 1 + 8;
        private const int U_OFF = Y_OFF + BPS * 16 + BPS;
        private const int V_OFF = U_OFF + 16;

        private sealed class Quant
        {
            public readonly int[] Y1 = new int[2];
            public readonly int[] Y2 = new int[2];
            public readonly int[] Uv = new int[2];
        }

        private sealed class MbData
        {
            public readonly short[] Coeffs = new short[384];
            public bool IsI4x4;
            public readonly byte[] Imodes = new byte[16];
            public byte Uvmode;
            public uint NonZeroY;
            public uint NonZeroUv;
            public bool Skip;
            public byte Segment;
        }

        private sealed class FInfo
        {
            public int Limit;     // f_limit_ (0 = no filtering)
            public int ILevel;    // f_ilevel_
            public bool Inner;    // f_inner_
            public int HevThresh; // hev_thresh_
        }

        // headers / state
        private int _width, _height, _mbW, _mbH;
        private bool _useSegment, _updateMap, _absoluteDelta;
        private readonly int[] _segQuant = new int[4];
        private readonly int[] _segFilter = new int[4];
        private readonly byte[] _segmentProb = { 255, 255, 255 };
        private bool _filterSimple;
        private int _filterLevel, _filterSharpness, _filterType;
        private bool _useLfDelta;
        private readonly int[] _refLfDelta = new int[4];
        private readonly int[] _modeLfDelta = new int[4];
        private bool _useSkipProba;
        private int _skipProb;
        private readonly Quant[] _dqm = { new Quant(), new Quant(), new Quant(), new Quant() };
        private byte[] _coeffProbas;      // [4*8*3*11]
        private readonly FInfo[,] _fstrength = new FInfo[4, 2];

        private Vp8BoolDecoder _br;
        private Vp8BoolDecoder[] _parts;
        private int _numPartsMinus1;

        // per-row working state
        private MbData[] _mbData;         // one per mb column
        private byte[] _nz, _nzDc;        // size mbW+1; index 0 = left, col c -> c+1
        private byte[] _intraT;           // 4*mbW top 4x4 modes
        private readonly byte[] _intraL = new byte[4];
        private byte[] _yuvB;             // per-MB working buffer
        private byte[][] _topY, _topU, _topV;  // saved top samples per column
        private bool[,] _skip;            // [mbW, mbH]: MB had no residuals
        private byte[,] _segMap;          // [mbW, mbH]: per-MB segment (for filtering)
        private bool[,] _i4Map;           // [mbW, mbH]: per-MB i4x4 flag (for filtering)

        // output planes (padded to MB grid)
        public byte[] PlaneY, PlaneU, PlaneV;
        public int StrideY, StrideUv;

        public static uint[] Decode(byte[] data, int offset, int length, out int width, out int height)
        {
            var dec = new Vp8Decoder();
            if (!dec.Run(data, offset, length)) { width = height = 0; return null; }
            width = dec._width;
            height = dec._height;
            return dec.ToArgb();
        }

        // Test-only: returns the decoded instance so callers can inspect the
        // raw YUV planes before upsampling/color conversion.
        public static Vp8Decoder DecodeInstance(byte[] data, int offset, int length)
        {
            var dec = new Vp8Decoder();
            return dec.Run(data, offset, length) ? dec : null;
        }

        public int Width => _width;
        public int Height => _height;

        private bool Run(byte[] data, int offset, int length)
        {
            if (length < 10) return false;
            uint bits = (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16));
            bool keyFrame = (bits & 1) == 0;
            int profile = (int)((bits >> 1) & 7);
            bool show = ((bits >> 4) & 1) != 0;
            int part0Len = (int)(bits >> 5);
            if (!keyFrame || profile > 3 || !show) return false;
            // start code 0x9d 0x01 0x2a
            if (data[offset + 3] != 0x9d || data[offset + 4] != 0x01 || data[offset + 5] != 0x2a) return false;
            _width = ((data[offset + 7] << 8) | data[offset + 6]) & 0x3fff;
            _height = ((data[offset + 9] << 8) | data[offset + 8]) & 0x3fff;
            if (_width == 0 || _height == 0) return false;
            _mbW = (_width + 15) >> 4;
            _mbH = (_height + 15) >> 4;

            int part0 = offset + 10;
            if (part0Len > length - 10) return false;
            _br = new Vp8BoolDecoder(data, part0, part0Len);

            // colorspace + clamp (ignored)
            _br.GetValue(1);
            _br.GetValue(1);

            ParseSegmentHeader();
            ParseFilterHeader();
            if (!ParsePartitions(data, part0 + part0Len, length - 10 - part0Len)) return false;
            ParseQuant();
            _br.GetValue(1);   // ignore update_proba flag value
            ParseProba();

            AllocAndDecode(data);
            return true;
        }

        // ---- header parsing ----
        private void ParseSegmentHeader()
        {
            _useSegment = _br.GetBit(0x80) != 0;
            if (_useSegment)
            {
                _updateMap = _br.GetBit(0x80) != 0;
                if (_br.GetBit(0x80) != 0)   // update data
                {
                    _absoluteDelta = _br.GetBit(0x80) != 0;
                    for (int s = 0; s < 4; ++s)
                        _segQuant[s] = _br.GetBit(0x80) != 0 ? _br.GetSignedValue(7) : 0;
                    for (int s = 0; s < 4; ++s)
                        _segFilter[s] = _br.GetBit(0x80) != 0 ? _br.GetSignedValue(6) : 0;
                }
                if (_updateMap)
                    for (int s = 0; s < 3; ++s)
                        _segmentProb[s] = (byte)(_br.GetBit(0x80) != 0 ? _br.GetValue(8) : 255);
            }
            else
            {
                _updateMap = false;
            }
        }

        private void ParseFilterHeader()
        {
            _filterSimple = _br.GetBit(0x80) != 0;
            _filterLevel = _br.GetValue(6);
            _filterSharpness = _br.GetValue(3);
            _useLfDelta = _br.GetBit(0x80) != 0;
            if (_useLfDelta && _br.GetBit(0x80) != 0)   // update lf-delta
            {
                for (int i = 0; i < 4; ++i)
                    if (_br.GetBit(0x80) != 0) _refLfDelta[i] = _br.GetSignedValue(6);
                for (int i = 0; i < 4; ++i)
                    if (_br.GetBit(0x80) != 0) _modeLfDelta[i] = _br.GetSignedValue(6);
            }
            _filterType = (_filterLevel == 0) ? 0 : (_filterSimple ? 1 : 2);
        }

        private bool ParsePartitions(byte[] data, int buf, int size)
        {
            _numPartsMinus1 = (1 << _br.GetValue(2)) - 1;
            int lastPart = _numPartsMinus1;
            if (size < 3 * lastPart) return false;
            int partStart = buf + lastPart * 3;
            int sizeLeft = size - lastPart * 3;
            _parts = new Vp8BoolDecoder[lastPart + 1];
            int sz = buf;
            for (int p = 0; p < lastPart; ++p)
            {
                int psize = data[sz] | (data[sz + 1] << 8) | (data[sz + 2] << 16);
                if (psize > sizeLeft) psize = sizeLeft;
                _parts[p] = new Vp8BoolDecoder(data, partStart, psize);
                partStart += psize;
                sizeLeft -= psize;
                sz += 3;
            }
            _parts[lastPart] = new Vp8BoolDecoder(data, partStart, sizeLeft);
            return sizeLeft >= 0;
        }

        private static int Clip(int v, int m) => v < 0 ? 0 : v > m ? m : v;

        private void ParseQuant()
        {
            int baseQ0 = _br.GetValue(7);
            int dqy1Dc = _br.GetBit(0x80) != 0 ? _br.GetSignedValue(4) : 0;
            int dqy2Dc = _br.GetBit(0x80) != 0 ? _br.GetSignedValue(4) : 0;
            int dqy2Ac = _br.GetBit(0x80) != 0 ? _br.GetSignedValue(4) : 0;
            int dquvDc = _br.GetBit(0x80) != 0 ? _br.GetSignedValue(4) : 0;
            int dquvAc = _br.GetBit(0x80) != 0 ? _br.GetSignedValue(4) : 0;

            for (int i = 0; i < 4; ++i)
            {
                int q;
                if (_useSegment)
                {
                    q = _segQuant[i];
                    if (!_absoluteDelta) q += baseQ0;
                }
                else
                {
                    if (i > 0) { CopyQuant(_dqm[i], _dqm[0]); continue; }
                    q = baseQ0;
                }
                var m = _dqm[i];
                m.Y1[0] = Vp8Tables.DcTable[Clip(q + dqy1Dc, 127)];
                m.Y1[1] = Vp8Tables.AcTable[Clip(q, 127)];
                m.Y2[0] = Vp8Tables.DcTable[Clip(q + dqy2Dc, 127)] * 2;
                m.Y2[1] = (Vp8Tables.AcTable[Clip(q + dqy2Ac, 127)] * 101581) >> 16;
                if (m.Y2[1] < 8) m.Y2[1] = 8;
                m.Uv[0] = Vp8Tables.DcTable[Clip(q + dquvDc, 117)];
                m.Uv[1] = Vp8Tables.AcTable[Clip(q + dquvAc, 127)];
            }
        }

        private static void CopyQuant(Quant d, Quant s)
        {
            Array.Copy(s.Y1, d.Y1, 2); Array.Copy(s.Y2, d.Y2, 2); Array.Copy(s.Uv, d.Uv, 2);
        }

        private static int Pidx(int t, int b, int c) => ((t * 8 + b) * 3 + c) * 11;

        private void ParseProba()
        {
            _coeffProbas = new byte[4 * 8 * 3 * 11];
            for (int t = 0; t < 4; ++t)
                for (int b = 0; b < 8; ++b)
                    for (int c = 0; c < 3; ++c)
                        for (int p = 0; p < 11; ++p)
                        {
                            int idx = Pidx(t, b, c) + p;
                            int v = _br.GetBit(Vp8Tables.CoeffsUpdateProba[idx]) != 0
                                ? _br.GetValue(8) : Vp8Tables.CoeffsProba0[idx];
                            _coeffProbas[idx] = (byte)v;
                        }
            _useSkipProba = _br.GetBit(0x80) != 0;
            if (_useSkipProba) _skipProb = _br.GetValue(8);
        }

        // ---- intra modes (paragraph 11) ----
        private void ParseIntraModeRow()
        {
            for (int mbX = 0; mbX < _mbW; ++mbX) ParseIntraMode(mbX);
        }

        private void ParseIntraMode(int mbX)
        {
            int topOff = 4 * mbX;
            var block = _mbData[mbX];

            block.Segment = (byte)(_updateMap
                ? (_br.GetBit(_segmentProb[0]) == 0
                    ? _br.GetBit(_segmentProb[1])
                    : _br.GetBit(_segmentProb[2]) + 2)
                : 0);
            block.Skip = _useSkipProba && _br.GetBit(_skipProb) != 0;

            block.IsI4x4 = _br.GetBit(145) == 0;
            if (!block.IsI4x4)
            {
                int ymode = _br.GetBit(156) != 0
                    ? (_br.GetBit(128) != 0 ? 1 /*TM*/ : 3 /*H*/)
                    : (_br.GetBit(163) != 0 ? 2 /*V*/ : 0 /*DC*/);
                block.Imodes[0] = (byte)ymode;
                for (int i = 0; i < 4; ++i) { _intraT[topOff + i] = (byte)ymode; _intraL[i] = (byte)ymode; }
            }
            else
            {
                for (int y = 0; y < 4; ++y)
                {
                    int ymode = _intraL[y];
                    for (int x = 0; x < 4; ++x)
                    {
                        int probBase = (_intraT[topOff + x] * 10 + ymode) * 9;
                        ymode = ParseBMode(probBase);
                        _intraT[topOff + x] = (byte)ymode;
                        block.Imodes[y * 4 + x] = (byte)ymode;
                    }
                    _intraL[y] = (byte)ymode;
                }
            }
            block.Uvmode = (byte)(_br.GetBit(142) == 0 ? 0
                : _br.GetBit(114) == 0 ? 2
                : _br.GetBit(183) != 0 ? 1 : 3);
        }

        private int ParseBMode(int p)
        {
            var pr = Vp8Tables.BModesProba;
            // Hardcoded I4x4 mode tree (RFC 6386 §11.2).
            if (_br.GetBit(pr[p + 0]) == 0) return 0;            // B_DC_PRED
            if (_br.GetBit(pr[p + 1]) == 0) return 1;            // B_TM_PRED
            if (_br.GetBit(pr[p + 2]) == 0) return 2;            // B_VE_PRED
            if (_br.GetBit(pr[p + 3]) == 0)
                return _br.GetBit(pr[p + 4]) == 0 ? 3            // B_HE_PRED
                     : _br.GetBit(pr[p + 5]) == 0 ? 4 : 5;       // B_RD / B_VR
            return _br.GetBit(pr[p + 6]) == 0 ? 6                // B_LD_PRED
                 : _br.GetBit(pr[p + 7]) == 0 ? 7                // B_VL_PRED
                 : _br.GetBit(pr[p + 8]) == 0 ? 8 : 9;           // B_HD / B_HU
        }

        // ---- residual decoding (paragraph 13) ----
        private int GetLargeValue(Vp8BoolDecoder br, int p)
        {
            var cp = _coeffProbas;
            int v;
            if (br.GetBit(cp[p + 3]) == 0)
            {
                v = br.GetBit(cp[p + 4]) == 0 ? 2 : 3 + br.GetBit(cp[p + 5]);
            }
            else if (br.GetBit(cp[p + 6]) == 0)
            {
                if (br.GetBit(cp[p + 7]) == 0) v = 5 + br.GetBit(159);
                else { v = 7 + 2 * br.GetBit(165); v += br.GetBit(145); }
            }
            else
            {
                int bit1 = br.GetBit(cp[p + 8]);
                int bit0 = br.GetBit(cp[p + 9 + bit1]);
                int cat = 2 * bit1 + bit0;
                byte[] tab = cat == 0 ? Vp8Tables.Cat3 : cat == 1 ? Vp8Tables.Cat4
                           : cat == 2 ? Vp8Tables.Cat5 : Vp8Tables.Cat6;
                v = 0;
                for (int k = 0; k < tab.Length; ++k) v += v + br.GetBit(tab[k]);
                v += 3 + (8 << cat);
            }
            return v;
        }

        private int GetCoeffs(Vp8BoolDecoder br, int type, int ctx, int[] dq, int n, short[] outc, int outOff)
        {
            var cp = _coeffProbas;
            int p = Pidx(type, Vp8Tables.Bands[n], ctx);
            for (; n < 16; ++n)
            {
                if (br.GetBit(cp[p + 0]) == 0) return n;
                while (br.GetBit(cp[p + 1]) == 0)
                {
                    p = Pidx(type, Vp8Tables.Bands[++n], 0);
                    if (n == 16) return 16;
                }
                int pCtx = Pidx(type, Vp8Tables.Bands[n + 1], 0);
                int v;
                if (br.GetBit(cp[p + 2]) == 0) { v = 1; p = pCtx + 11; }
                else { v = GetLargeValue(br, p); p = pCtx + 22; }
                outc[outOff + Vp8Tables.Zigzag[n]] = (short)(br.GetSigned(v) * dq[n > 0 ? 1 : 0]);
            }
            return 16;
        }

        private static uint NzCodeBits(uint nz, int n, int dcNz)
            => (nz << 2) | (uint)(n > 3 ? 3 : n > 1 ? 2 : dcNz);

        private bool ParseResiduals(int mbX, Vp8BoolDecoder tokenBr)
        {
            var block = _mbData[mbX];
            var q = _dqm[block.Segment];
            var dst = block.Coeffs;
            Array.Clear(dst, 0, 384);

            int mbIdx = mbX + 1;   // _nz/_nzDc index for this column
            uint nonZeroY = 0, nonZeroUv = 0;
            int first, acType;

            if (!block.IsI4x4)   // parse the Y2 (DC) block via WHT
            {
                var dc = new short[16];
                int ctx = _nzDc[mbIdx] + _nzDc[0];
                int nz = GetCoeffs(tokenBr, 1, ctx, q.Y2, 0, dc, 0);
                _nzDc[mbIdx] = _nzDc[0] = (byte)(nz > 0 ? 1 : 0);
                if (nz > 1) Vp8Dsp.TransformWHT(dc, 0, dst, 0);
                else
                {
                    int dc0 = (dc[0] + 3) >> 3;
                    for (int i = 0; i < 16 * 16; i += 16) dst[i] = (short)dc0;
                }
                first = 1; acType = 0;
            }
            else { first = 0; acType = 3; }

            int tnz = _nz[mbIdx] & 0x0f;
            int lnz = _nz[0] & 0x0f;
            int dstOff = 0;
            for (int y = 0; y < 4; ++y)
            {
                int l = lnz & 1;
                uint nzCoeffs = 0;
                for (int x = 0; x < 4; ++x)
                {
                    int ctx = l + (tnz & 1);
                    int nz = GetCoeffs(tokenBr, acType, ctx, q.Y1, first, dst, dstOff);
                    l = nz > first ? 1 : 0;
                    tnz = (tnz >> 1) | (l << 7);
                    nzCoeffs = NzCodeBits(nzCoeffs, nz, dst[dstOff] != 0 ? 1 : 0);
                    dstOff += 16;
                }
                tnz >>= 4;
                lnz = (lnz >> 1) | (l << 7);
                nonZeroY = (nonZeroY << 8) | nzCoeffs;
            }
            uint outTnz = (uint)tnz;
            uint outLnz = (uint)(lnz >> 4);

            for (int ch = 0; ch < 4; ch += 2)
            {
                uint nzCoeffs = 0;
                tnz = _nz[mbIdx] >> (4 + ch);
                lnz = _nz[0] >> (4 + ch);
                for (int y = 0; y < 2; ++y)
                {
                    int l = lnz & 1;
                    for (int x = 0; x < 2; ++x)
                    {
                        int ctx = l + (tnz & 1);
                        int nz = GetCoeffs(tokenBr, 2, ctx, q.Uv, 0, dst, dstOff);
                        l = nz > 0 ? 1 : 0;
                        tnz = (tnz >> 1) | (l << 3);
                        nzCoeffs = NzCodeBits(nzCoeffs, nz, dst[dstOff] != 0 ? 1 : 0);
                        dstOff += 16;
                    }
                    tnz >>= 2;
                    lnz = (lnz >> 1) | (l << 5);
                }
                nonZeroUv |= nzCoeffs << (4 * ch);
                outTnz |= (uint)((tnz << 4) << ch);
                outLnz |= (uint)((lnz & 0xf0) << ch);
            }
            _nz[mbIdx] = (byte)outTnz;
            _nz[0] = (byte)outLnz;

            block.NonZeroY = nonZeroY;
            block.NonZeroUv = nonZeroUv;
            return (nonZeroY | nonZeroUv) == 0;
        }

        // ---- frame allocation + main loop ----
        private void AllocAndDecode(byte[] data)
        {
            _mbData = new MbData[_mbW];
            for (int i = 0; i < _mbW; ++i) _mbData[i] = new MbData();
            _nz = new byte[_mbW + 1];
            _nzDc = new byte[_mbW + 1];
            _intraT = new byte[4 * _mbW];
            _yuvB = new byte[YUV_SIZE];
            _topY = new byte[_mbW][];
            _topU = new byte[_mbW][];
            _topV = new byte[_mbW][];
            for (int i = 0; i < _mbW; ++i) { _topY[i] = new byte[16]; _topU[i] = new byte[8]; _topV[i] = new byte[8]; }

            _skip = new bool[_mbW, _mbH];
            _segMap = new byte[_mbW, _mbH];
            _i4Map = new bool[_mbW, _mbH];
            StrideY = _mbW * 16;
            StrideUv = _mbW * 8;
            PlaneY = new byte[StrideY * _mbH * 16];
            PlaneU = new byte[StrideUv * _mbH * 8];
            PlaneV = new byte[StrideUv * _mbH * 8];

            PrecomputeFilterStrengths();

            for (int mbY = 0; mbY < _mbH; ++mbY)
            {
                var tokenBr = _parts[mbY & _numPartsMinus1];
                // reset left contexts for the scanline
                _nz[0] = 0; _nzDc[0] = 0;
                for (int i = 0; i < 4; ++i) _intraL[i] = 0;   // B_DC_PRED

                ParseIntraModeRow();
                for (int mbX = 0; mbX < _mbW; ++mbX)
                {
                    bool skip = _useSkipProba && _mbData[mbX].Skip;
                    if (!skip) skip = ParseResiduals(mbX, tokenBr);
                    else
                    {
                        _nz[0] = _nz[mbX + 1] = 0;
                        if (!_mbData[mbX].IsI4x4) { _nzDc[0] = _nzDc[mbX + 1] = 0; }
                        _mbData[mbX].NonZeroY = 0;
                        _mbData[mbX].NonZeroUv = 0;
                    }
                    _skip[mbX, mbY] = skip;
                    _segMap[mbX, mbY] = _mbData[mbX].Segment;
                    _i4Map[mbX, mbY] = _mbData[mbX].IsI4x4;
                }
                ReconstructRow(mbY);
            }

            FilterFrame();
        }

        // ---- reconstruction ----
        private void ReconstructRow(int mbY)
        {
            var yb = _yuvB;
            int yDst = Y_OFF, uDst = U_OFF, vDst = V_OFF;

            for (int j = 0; j < 16; ++j) yb[yDst + j * BPS - 1] = 129;
            for (int j = 0; j < 8; ++j) { yb[uDst + j * BPS - 1] = 129; yb[vDst + j * BPS - 1] = 129; }
            if (mbY > 0)
            {
                yb[yDst - 1 - BPS] = 129; yb[uDst - 1 - BPS] = 129; yb[vDst - 1 - BPS] = 129;
            }
            else
            {
                for (int i = 0; i < 16 + 4 + 1; ++i) yb[yDst - BPS - 1 + i] = 127;
                for (int i = 0; i < 8 + 1; ++i) { yb[uDst - BPS - 1 + i] = 127; yb[vDst - BPS - 1 + i] = 127; }
            }

            for (int mbX = 0; mbX < _mbW; ++mbX)
            {
                var block = _mbData[mbX];

                if (mbX > 0)
                {
                    for (int j = -1; j < 16; ++j) Copy4(yb, yDst + j * BPS - 4, yb, yDst + j * BPS + 12);
                    for (int j = -1; j < 8; ++j)
                    {
                        Copy4(yb, uDst + j * BPS - 4, yb, uDst + j * BPS + 4);
                        Copy4(yb, vDst + j * BPS - 4, yb, vDst + j * BPS + 4);
                    }
                }

                if (mbY > 0)
                {
                    Array.Copy(_topY[mbX], 0, yb, yDst - BPS, 16);
                    Array.Copy(_topU[mbX], 0, yb, uDst - BPS, 8);
                    Array.Copy(_topV[mbX], 0, yb, vDst - BPS, 8);
                }

                var coeffs = block.Coeffs;
                uint bits = block.NonZeroY;

                if (block.IsI4x4)
                {
                    int tr = yDst - BPS + 16;   // top-right 4 samples
                    if (mbY > 0)
                    {
                        if (mbX >= _mbW - 1)
                            for (int i = 0; i < 4; ++i) yb[tr + i] = _topY[mbX][15];
                        else
                            Array.Copy(_topY[mbX + 1], 0, yb, tr, 4);
                    }
                    // replicate top-right below (rows 3,7,11 within the MB)
                    for (int k = 1; k <= 3; ++k) Copy4(yb, tr + k * 4 * BPS, yb, tr);

                    for (int n = 0; n < 16; ++n, bits <<= 2)
                    {
                        int d = yDst + Vp8Tables.Scan[n];
                        Vp8Dsp.PredLuma4(block.Imodes[n], yb, d);
                        DoTransform(bits, coeffs, n * 16, yb, d);
                    }
                }
                else
                {
                    int predFunc = CheckMode(mbX, mbY, block.Imodes[0]);
                    Vp8Dsp.PredLuma16(predFunc, yb, yDst);
                    if (bits != 0)
                        for (int n = 0; n < 16; ++n, bits <<= 2)
                            DoTransform(bits, coeffs, n * 16, yb, yDst + Vp8Tables.Scan[n]);
                }

                uint bitsUv = block.NonZeroUv;
                int predUv = CheckMode(mbX, mbY, block.Uvmode);
                Vp8Dsp.PredChroma8(predUv, yb, uDst);
                Vp8Dsp.PredChroma8(predUv, yb, vDst);
                DoUVTransform(bitsUv, coeffs, 16 * 16, yb, uDst);
                DoUVTransform(bitsUv >> 8, coeffs, 20 * 16, yb, vDst);

                if (mbY < _mbH - 1)
                {
                    Array.Copy(yb, yDst + 15 * BPS, _topY[mbX], 0, 16);
                    Array.Copy(yb, uDst + 7 * BPS, _topU[mbX], 0, 8);
                    Array.Copy(yb, vDst + 7 * BPS, _topV[mbX], 0, 8);
                }

                // copy block to output planes
                int yOut = mbX * 16 + mbY * 16 * StrideY;
                for (int j = 0; j < 16; ++j) Array.Copy(yb, yDst + j * BPS, PlaneY, yOut + j * StrideY, 16);
                int uvOut = mbX * 8 + mbY * 8 * StrideUv;
                for (int j = 0; j < 8; ++j)
                {
                    Array.Copy(yb, uDst + j * BPS, PlaneU, uvOut + j * StrideUv, 8);
                    Array.Copy(yb, vDst + j * BPS, PlaneV, uvOut + j * StrideUv, 8);
                }
            }
        }

        private static void Copy4(byte[] dst, int d, byte[] src, int s)
        {
            dst[d] = src[s]; dst[d + 1] = src[s + 1]; dst[d + 2] = src[s + 2]; dst[d + 3] = src[s + 3];
        }

        private static int CheckMode(int mbX, int mbY, int mode)
        {
            if (mode == 0)   // B_DC_PRED
            {
                if (mbX == 0) return mbY == 0 ? 6 : 5;   // NOTOPLEFT : NOLEFT
                return mbY == 0 ? 4 : 0;                 // NOTOP : DC
            }
            return mode;
        }

        private static void DoTransform(uint bits, short[] src, int srcOff, byte[] dst, int d)
        {
            switch (bits >> 30)
            {
                case 3: Vp8Dsp.Transform(src, srcOff, dst, d, false); break;
                case 2: Vp8Dsp.TransformAC3(src, srcOff, dst, d); break;
                case 1: Vp8Dsp.TransformDC(src, srcOff, dst, d); break;
            }
        }

        private static void DoUVTransform(uint bits, short[] src, int srcOff, byte[] dst, int d)
        {
            if ((bits & 0xff) != 0)
            {
                if ((bits & 0xaa) != 0) Vp8Dsp.TransformUV(src, srcOff, dst, d);
                else Vp8Dsp.TransformDCUV(src, srcOff, dst, d);
            }
        }

        // ---- filtering ----
        private void PrecomputeFilterStrengths()
        {
            for (int s = 0; s < 4; ++s)
                for (int i4 = 0; i4 < 2; ++i4)
                    _fstrength[s, i4] = new FInfo();
            if (_filterType == 0) return;

            for (int s = 0; s < 4; ++s)
            {
                int baseLevel;
                if (_useSegment)
                {
                    baseLevel = _segFilter[s];
                    if (!_absoluteDelta) baseLevel += _filterLevel;
                }
                else baseLevel = _filterLevel;

                for (int i4 = 0; i4 <= 1; ++i4)
                {
                    var info = _fstrength[s, i4];
                    int level = baseLevel;
                    if (_useLfDelta)
                    {
                        level += _refLfDelta[0];
                        if (i4 == 1) level += _modeLfDelta[0];
                    }
                    level = level < 0 ? 0 : level > 63 ? 63 : level;
                    if (level > 0)
                    {
                        int ilevel = level;
                        if (_filterSharpness > 0)
                        {
                            ilevel = _filterSharpness > 4 ? (ilevel >> 2) : (ilevel >> 1);
                            if (ilevel > 9 - _filterSharpness) ilevel = 9 - _filterSharpness;
                        }
                        if (ilevel < 1) ilevel = 1;
                        info.ILevel = ilevel;
                        info.Limit = 2 * level + ilevel;
                        info.HevThresh = level >= 40 ? 2 : level >= 15 ? 1 : 0;
                    }
                    else info.Limit = 0;
                    info.Inner = i4 == 1;
                }
            }
        }

        private void FilterFrame()
        {
            if (_filterType == 0) return;
            for (int mbY = 0; mbY < _mbH; ++mbY)
                for (int mbX = 0; mbX < _mbW; ++mbX)
                    DoFilter(mbX, mbY);
        }

        private void DoFilter(int mbX, int mbY)
        {
            bool isI4 = _i4Map[mbX, mbY];
            var f = _fstrength[_segMap[mbX, mbY], isI4 ? 1 : 0];
            int limit = f.Limit;
            if (limit == 0) return;
            // inner edges are filtered for i4x4 MBs or any MB carrying residuals
            bool inner = f.Inner || !_skip[mbX, mbY];

            int yBps = StrideY;
            int yOff = mbY * 16 * yBps + mbX * 16;
            int ilevel = f.ILevel;

            if (_filterType == 1)   // simple (luma only)
            {
                if (mbX > 0) Vp8Dsp.SimpleHFilter16(PlaneY, yOff, yBps, limit + 4);
                if (inner) Vp8Dsp.SimpleHFilter16i(PlaneY, yOff, yBps, limit);
                if (mbY > 0) Vp8Dsp.SimpleVFilter16(PlaneY, yOff, yBps, limit + 4);
                if (inner) Vp8Dsp.SimpleVFilter16i(PlaneY, yOff, yBps, limit);
            }
            else                    // complex (luma + chroma)
            {
                int uvBps = StrideUv;
                int uvOff = mbY * 8 * uvBps + mbX * 8;
                int hev = f.HevThresh;
                if (mbX > 0)
                {
                    Vp8Dsp.HFilter16(PlaneY, yOff, yBps, limit + 4, ilevel, hev);
                    Vp8Dsp.HFilter8(PlaneU, uvOff, PlaneV, uvOff, uvBps, limit + 4, ilevel, hev);
                }
                if (inner)
                {
                    Vp8Dsp.HFilter16i(PlaneY, yOff, yBps, limit, ilevel, hev);
                    Vp8Dsp.HFilter8i(PlaneU, uvOff, PlaneV, uvOff, uvBps, limit, ilevel, hev);
                }
                if (mbY > 0)
                {
                    Vp8Dsp.VFilter16(PlaneY, yOff, yBps, limit + 4, ilevel, hev);
                    Vp8Dsp.VFilter8(PlaneU, uvOff, PlaneV, uvOff, uvBps, limit + 4, ilevel, hev);
                }
                if (inner)
                {
                    Vp8Dsp.VFilter16i(PlaneY, yOff, yBps, limit, ilevel, hev);
                    Vp8Dsp.VFilter8i(PlaneU, uvOff, PlaneV, uvOff, uvBps, limit, ilevel, hev);
                }
            }
        }

        // ---- YUV -> ARGB ----
        private static int MultHi(int v, int c) => (v * c) >> 8;
        private static int Clip8(int v) => (v & ~16383) == 0 ? (v >> 6) : (v < 0 ? 0 : 255);

        private uint[] ToArgb()
        {
            int w = _width, h = _height;
            var argb = new uint[w * h];
            for (int y = 0; y < h; ++y)
            {
                int yRow = y * StrideY;
                int uvRow = (y >> 1) * StrideUv;
                int outRow = y * w;
                for (int x = 0; x < w; ++x)
                {
                    int Y = PlaneY[yRow + x];
                    int U = PlaneU[uvRow + (x >> 1)];
                    int V = PlaneV[uvRow + (x >> 1)];
                    int r = Clip8(MultHi(Y, 19077) + MultHi(V, 26149) - 14234);
                    int g = Clip8(MultHi(Y, 19077) - MultHi(U, 6419) - MultHi(V, 13320) + 8708);
                    int b = Clip8(MultHi(Y, 19077) + MultHi(U, 33050) - 17685);
                    argb[outRow + x] = 0xff000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
                }
            }
            return argb;
        }
    }
}
