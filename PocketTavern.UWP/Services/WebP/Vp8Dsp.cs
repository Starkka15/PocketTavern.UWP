namespace PocketTavern.UWP.Services.WebP
{
    // VP8 pixel operations (port of libwebp src/dsp/dec.c): intra prediction,
    // inverse transforms, and the in-loop deblocking filters.
    //
    // Predictors and transforms operate on a per-macroblock working buffer with
    // a fixed stride of BPS (32). Filters take a runtime stride so they can run
    // over the full-frame planes.
    internal static class Vp8Dsp
    {
        public const int BPS = 32;

        // ---- clip helpers (replace libwebp's precomputed clip tables) ----
        private static int Abs0(int x) => x < 0 ? -x : x;
        private static int Sclip1(int x) => x < -128 ? -128 : x > 127 ? 127 : x;   // [-1020,1020]->[-128,127]
        private static int Sclip2(int x) => x < -16 ? -16 : x > 15 ? 15 : x;       // [-112,112]->[-16,15]
        private static byte Clip1(int x) => (byte)(x < 0 ? 0 : x > 255 ? 255 : x); // [-255,511]->[0,255]
        private static byte Clip8b(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

        private static int Avg3(int a, int b, int c) => (a + 2 * b + c + 2) >> 2;
        private static int Avg2(int a, int b) => (a + b + 1) >> 1;

        // ====================================================================
        // Inverse transforms

        private static int Mul1(int a) => ((a * 20091) >> 16) + a;
        private static int Mul2(int a) => (a * 35468) >> 16;

        private static void TransformOne(short[] inp, int o, byte[] dst, int d)
        {
            var C = new int[16];
            int t = 0, ip = o;
            for (int i = 0; i < 4; ++i)   // vertical pass
            {
                int a = inp[ip + 0] + inp[ip + 8];
                int b = inp[ip + 0] - inp[ip + 8];
                int c = Mul2(inp[ip + 4]) - Mul1(inp[ip + 12]);
                int dd = Mul1(inp[ip + 4]) + Mul2(inp[ip + 12]);
                C[t + 0] = a + dd;
                C[t + 1] = b + c;
                C[t + 2] = b - c;
                C[t + 3] = a - dd;
                t += 4; ip++;
            }
            t = 0;
            for (int i = 0; i < 4; ++i)   // horizontal pass
            {
                int dc = C[t + 0] + 4;
                int a = dc + C[t + 8];
                int b = dc - C[t + 8];
                int c = Mul2(C[t + 4]) - Mul1(C[t + 12]);
                int dd = Mul1(C[t + 4]) + Mul2(C[t + 12]);
                Store(dst, d, 0, 0, a + dd);
                Store(dst, d, 1, 0, b + c);
                Store(dst, d, 2, 0, b - c);
                Store(dst, d, 3, 0, a - dd);
                t++; d += BPS;
            }
        }

        private static void Store(byte[] dst, int d, int x, int y, int v)
        {
            int idx = d + x + y * BPS;
            dst[idx] = Clip8b(dst[idx] + (v >> 3));
        }

        public static void Transform(short[] inp, int o, byte[] dst, int d, bool doTwo)
        {
            TransformOne(inp, o, dst, d);
            if (doTwo) TransformOne(inp, o + 16, dst, d + 4);
        }

        public static void TransformDC(short[] inp, int o, byte[] dst, int d)
        {
            int dc = inp[o] + 4;
            for (int j = 0; j < 4; ++j)
                for (int i = 0; i < 4; ++i)
                    Store(dst, d, i, j, dc);
        }

        public static void TransformAC3(short[] inp, int o, byte[] dst, int d)
        {
            int a = inp[o + 0] + 4;
            int c4 = Mul2(inp[o + 4]);
            int d4 = Mul1(inp[o + 4]);
            int c1 = Mul2(inp[o + 1]);
            int d1 = Mul1(inp[o + 1]);
            Store2(dst, d, 0, a + d4, d1, c1);
            Store2(dst, d, 1, a + c4, d1, c1);
            Store2(dst, d, 2, a - c4, d1, c1);
            Store2(dst, d, 3, a - d4, d1, c1);
        }

        private static void Store2(byte[] dst, int d, int y, int dc, int dd, int c)
        {
            Store(dst, d, 0, y, dc + dd);
            Store(dst, d, 1, y, dc + c);
            Store(dst, d, 2, y, dc - c);
            Store(dst, d, 3, y, dc - dd);
        }

        public static void TransformUV(short[] inp, int o, byte[] dst, int d)
        {
            Transform(inp, o + 0 * 16, dst, d, true);
            Transform(inp, o + 2 * 16, dst, d + 4 * BPS, true);
        }

        public static void TransformDCUV(short[] inp, int o, byte[] dst, int d)
        {
            if (inp[o + 0 * 16] != 0) TransformDC(inp, o + 0 * 16, dst, d);
            if (inp[o + 1 * 16] != 0) TransformDC(inp, o + 1 * 16, dst, d + 4);
            if (inp[o + 2 * 16] != 0) TransformDC(inp, o + 2 * 16, dst, d + 4 * BPS);
            if (inp[o + 3 * 16] != 0) TransformDC(inp, o + 3 * 16, dst, d + 4 * BPS + 4);
        }

        public static void TransformWHT(short[] inp, int o, short[] outp, int outO)
        {
            var tmp = new int[16];
            for (int i = 0; i < 4; ++i)
            {
                int a0 = inp[o + 0 + i] + inp[o + 12 + i];
                int a1 = inp[o + 4 + i] + inp[o + 8 + i];
                int a2 = inp[o + 4 + i] - inp[o + 8 + i];
                int a3 = inp[o + 0 + i] - inp[o + 12 + i];
                tmp[0 + i] = a0 + a1;
                tmp[8 + i] = a0 - a1;
                tmp[4 + i] = a3 + a2;
                tmp[12 + i] = a3 - a2;
            }
            int op = outO;
            for (int i = 0; i < 4; ++i)
            {
                int dc = tmp[0 + i * 4] + 3;
                int a0 = dc + tmp[3 + i * 4];
                int a1 = tmp[1 + i * 4] + tmp[2 + i * 4];
                int a2 = tmp[1 + i * 4] - tmp[2 + i * 4];
                int a3 = dc - tmp[3 + i * 4];
                outp[op + 0] = (short)((a0 + a1) >> 3);
                outp[op + 16] = (short)((a3 + a2) >> 3);
                outp[op + 32] = (short)((a0 - a1) >> 3);
                outp[op + 48] = (short)((a3 - a2) >> 3);
                op += 64;
            }
        }

        // ====================================================================
        // Intra prediction

        private static void TrueMotion(byte[] d, int off, int size)
        {
            int top = off - BPS;   // fixed: always the MB's top border row
            int tl = d[top - 1];   // top-left sample
            for (int y = 0; y < size; ++y)
            {
                int rowOff = off + y * BPS;
                int left = d[rowOff - 1];
                for (int x = 0; x < size; ++x)
                    d[rowOff + x] = Clip1(d[top + x] + left - tl);
            }
        }

        public static void PredLuma16(int mode, byte[] d, int off)
        {
            switch (mode)
            {
                case 0: DC16(d, off); break;
                case 1: TrueMotion(d, off, 16); break;
                case 2: VE16(d, off); break;
                case 3: HE16(d, off); break;
                case 4: DC16NoTop(d, off); break;
                case 5: DC16NoLeft(d, off); break;
                case 6: Put16(0x80, d, off); break;
            }
        }

        public static void PredChroma8(int mode, byte[] d, int off)
        {
            switch (mode)
            {
                case 0: DC8(d, off); break;
                case 1: TrueMotion(d, off, 8); break;
                case 2: VE8(d, off); break;
                case 3: HE8(d, off); break;
                case 4: DC8NoTop(d, off); break;
                case 5: DC8NoLeft(d, off); break;
                case 6: Put8(0x80, d, off); break;
            }
        }

        private static void Put16(int v, byte[] d, int off)
        {
            for (int j = 0; j < 16; ++j)
                for (int i = 0; i < 16; ++i) d[off + j * BPS + i] = (byte)v;
        }

        private static void VE16(byte[] d, int off)
        {
            for (int j = 0; j < 16; ++j)
                for (int i = 0; i < 16; ++i) d[off + j * BPS + i] = d[off - BPS + i];
        }

        private static void HE16(byte[] d, int off)
        {
            for (int j = 0; j < 16; ++j)
            {
                byte v = d[off + j * BPS - 1];
                for (int i = 0; i < 16; ++i) d[off + j * BPS + i] = v;
            }
        }

        private static void DC16(byte[] d, int off)
        {
            int dc = 16;
            for (int j = 0; j < 16; ++j) dc += d[off - 1 + j * BPS] + d[off + j - BPS];
            Put16(dc >> 5, d, off);
        }

        private static void DC16NoTop(byte[] d, int off)
        {
            int dc = 8;
            for (int j = 0; j < 16; ++j) dc += d[off - 1 + j * BPS];
            Put16(dc >> 4, d, off);
        }

        private static void DC16NoLeft(byte[] d, int off)
        {
            int dc = 8;
            for (int i = 0; i < 16; ++i) dc += d[off + i - BPS];
            Put16(dc >> 4, d, off);
        }

        private static void Put8(int v, byte[] d, int off)
        {
            for (int j = 0; j < 8; ++j)
                for (int i = 0; i < 8; ++i) d[off + j * BPS + i] = (byte)v;
        }

        private static void VE8(byte[] d, int off)
        {
            for (int j = 0; j < 8; ++j)
                for (int i = 0; i < 8; ++i) d[off + j * BPS + i] = d[off - BPS + i];
        }

        private static void HE8(byte[] d, int off)
        {
            for (int j = 0; j < 8; ++j)
            {
                byte v = d[off + j * BPS - 1];
                for (int i = 0; i < 8; ++i) d[off + j * BPS + i] = v;
            }
        }

        private static void DC8(byte[] d, int off)
        {
            int dc = 8;
            for (int i = 0; i < 8; ++i) dc += d[off + i - BPS] + d[off - 1 + i * BPS];
            Put8(dc >> 4, d, off);
        }

        private static void DC8NoLeft(byte[] d, int off)
        {
            int dc = 4;
            for (int i = 0; i < 8; ++i) dc += d[off + i - BPS];
            Put8(dc >> 3, d, off);
        }

        private static void DC8NoTop(byte[] d, int off)
        {
            int dc = 4;
            for (int i = 0; i < 8; ++i) dc += d[off - 1 + i * BPS];
            Put8(dc >> 3, d, off);
        }

        // ---- 4x4 luma predictors ----
        private static int D(byte[] d, int off, int x, int y) => d[off + x + y * BPS];
        private static void S(byte[] d, int off, int x, int y, int v) => d[off + x + y * BPS] = (byte)v;

        public static void PredLuma4(int mode, byte[] d, int off)
        {
            switch (mode)
            {
                case 0: DC4(d, off); break;
                case 1: TrueMotion(d, off, 4); break;
                case 2: VE4(d, off); break;
                case 3: HE4(d, off); break;
                case 4: RD4(d, off); break;
                case 5: VR4(d, off); break;
                case 6: LD4(d, off); break;
                case 7: VL4(d, off); break;
                case 8: HD4(d, off); break;
                case 9: HU4(d, off); break;
            }
        }

        private static void DC4(byte[] d, int off)
        {
            int dc = 4;
            for (int i = 0; i < 4; ++i) dc += d[off + i - BPS] + d[off - 1 + i * BPS];
            dc >>= 3;
            for (int j = 0; j < 4; ++j)
                for (int i = 0; i < 4; ++i) d[off + i + j * BPS] = (byte)dc;
        }

        private static void VE4(byte[] d, int off)
        {
            int t = off - BPS;
            byte[] v = {
                (byte)Avg3(d[t - 1], d[t + 0], d[t + 1]),
                (byte)Avg3(d[t + 0], d[t + 1], d[t + 2]),
                (byte)Avg3(d[t + 1], d[t + 2], d[t + 3]),
                (byte)Avg3(d[t + 2], d[t + 3], d[t + 4])
            };
            for (int j = 0; j < 4; ++j)
                for (int i = 0; i < 4; ++i) d[off + j * BPS + i] = v[i];
        }

        private static void HE4(byte[] d, int off)
        {
            int A = d[off - 1 - BPS], B = d[off - 1], C = d[off - 1 + BPS];
            int Dd = d[off - 1 + 2 * BPS], E = d[off - 1 + 3 * BPS];
            FillRow(d, off + 0 * BPS, Avg3(A, B, C));
            FillRow(d, off + 1 * BPS, Avg3(B, C, Dd));
            FillRow(d, off + 2 * BPS, Avg3(C, Dd, E));
            FillRow(d, off + 3 * BPS, Avg3(Dd, E, E));
        }

        private static void FillRow(byte[] d, int o, int v)
        {
            d[o] = d[o + 1] = d[o + 2] = d[o + 3] = (byte)v;
        }

        private static void RD4(byte[] d, int off)
        {
            int I = D(d, off, -1, 0), J = D(d, off, -1, 1), K = D(d, off, -1, 2), L = D(d, off, -1, 3);
            int X = D(d, off, -1, -1), A = D(d, off, 0, -1), B = D(d, off, 1, -1), C = D(d, off, 2, -1), Dd = D(d, off, 3, -1);
            S(d, off, 0, 3, Avg3(J, K, L));
            S(d, off, 1, 3, Avg3(I, J, K)); S(d, off, 0, 2, Avg3(I, J, K));
            S(d, off, 2, 3, Avg3(X, I, J)); S(d, off, 1, 2, Avg3(X, I, J)); S(d, off, 0, 1, Avg3(X, I, J));
            S(d, off, 3, 3, Avg3(A, X, I)); S(d, off, 2, 2, Avg3(A, X, I)); S(d, off, 1, 1, Avg3(A, X, I)); S(d, off, 0, 0, Avg3(A, X, I));
            S(d, off, 3, 2, Avg3(B, A, X)); S(d, off, 2, 1, Avg3(B, A, X)); S(d, off, 1, 0, Avg3(B, A, X));
            S(d, off, 3, 1, Avg3(C, B, A)); S(d, off, 2, 0, Avg3(C, B, A));
            S(d, off, 3, 0, Avg3(Dd, C, B));
        }

        private static void LD4(byte[] d, int off)
        {
            int A = D(d, off, 0, -1), B = D(d, off, 1, -1), C = D(d, off, 2, -1), Dd = D(d, off, 3, -1);
            int E = D(d, off, 4, -1), F = D(d, off, 5, -1), G = D(d, off, 6, -1), H = D(d, off, 7, -1);
            S(d, off, 0, 0, Avg3(A, B, C));
            S(d, off, 1, 0, Avg3(B, C, Dd)); S(d, off, 0, 1, Avg3(B, C, Dd));
            S(d, off, 2, 0, Avg3(C, Dd, E)); S(d, off, 1, 1, Avg3(C, Dd, E)); S(d, off, 0, 2, Avg3(C, Dd, E));
            S(d, off, 3, 0, Avg3(Dd, E, F)); S(d, off, 2, 1, Avg3(Dd, E, F)); S(d, off, 1, 2, Avg3(Dd, E, F)); S(d, off, 0, 3, Avg3(Dd, E, F));
            S(d, off, 3, 1, Avg3(E, F, G)); S(d, off, 2, 2, Avg3(E, F, G)); S(d, off, 1, 3, Avg3(E, F, G));
            S(d, off, 3, 2, Avg3(F, G, H)); S(d, off, 2, 3, Avg3(F, G, H));
            S(d, off, 3, 3, Avg3(G, H, H));
        }

        private static void VR4(byte[] d, int off)
        {
            int I = D(d, off, -1, 0), J = D(d, off, -1, 1), K = D(d, off, -1, 2);
            int X = D(d, off, -1, -1), A = D(d, off, 0, -1), B = D(d, off, 1, -1), C = D(d, off, 2, -1), Dd = D(d, off, 3, -1);
            S(d, off, 0, 0, Avg2(X, A)); S(d, off, 1, 2, Avg2(X, A));
            S(d, off, 1, 0, Avg2(A, B)); S(d, off, 2, 2, Avg2(A, B));
            S(d, off, 2, 0, Avg2(B, C)); S(d, off, 3, 2, Avg2(B, C));
            S(d, off, 3, 0, Avg2(C, Dd));
            S(d, off, 0, 3, Avg3(K, J, I));
            S(d, off, 0, 2, Avg3(J, I, X));
            S(d, off, 0, 1, Avg3(I, X, A)); S(d, off, 1, 3, Avg3(I, X, A));
            S(d, off, 1, 1, Avg3(X, A, B)); S(d, off, 2, 3, Avg3(X, A, B));
            S(d, off, 2, 1, Avg3(A, B, C)); S(d, off, 3, 3, Avg3(A, B, C));
            S(d, off, 3, 1, Avg3(B, C, Dd));
        }

        private static void VL4(byte[] d, int off)
        {
            int A = D(d, off, 0, -1), B = D(d, off, 1, -1), C = D(d, off, 2, -1), Dd = D(d, off, 3, -1);
            int E = D(d, off, 4, -1), F = D(d, off, 5, -1), G = D(d, off, 6, -1), H = D(d, off, 7, -1);
            S(d, off, 0, 0, Avg2(A, B));
            S(d, off, 1, 0, Avg2(B, C)); S(d, off, 0, 2, Avg2(B, C));
            S(d, off, 2, 0, Avg2(C, Dd)); S(d, off, 1, 2, Avg2(C, Dd));
            S(d, off, 3, 0, Avg2(Dd, E)); S(d, off, 2, 2, Avg2(Dd, E));
            S(d, off, 0, 1, Avg3(A, B, C));
            S(d, off, 1, 1, Avg3(B, C, Dd)); S(d, off, 0, 3, Avg3(B, C, Dd));
            S(d, off, 2, 1, Avg3(C, Dd, E)); S(d, off, 1, 3, Avg3(C, Dd, E));
            S(d, off, 3, 1, Avg3(Dd, E, F)); S(d, off, 2, 3, Avg3(Dd, E, F));
            S(d, off, 3, 2, Avg3(E, F, G));
            S(d, off, 3, 3, Avg3(F, G, H));
        }

        private static void HU4(byte[] d, int off)
        {
            int I = D(d, off, -1, 0), J = D(d, off, -1, 1), K = D(d, off, -1, 2), L = D(d, off, -1, 3);
            S(d, off, 0, 0, Avg2(I, J));
            S(d, off, 2, 0, Avg2(J, K)); S(d, off, 0, 1, Avg2(J, K));
            S(d, off, 2, 1, Avg2(K, L)); S(d, off, 0, 2, Avg2(K, L));
            S(d, off, 1, 0, Avg3(I, J, K));
            S(d, off, 3, 0, Avg3(J, K, L)); S(d, off, 1, 1, Avg3(J, K, L));
            S(d, off, 3, 1, Avg3(K, L, L)); S(d, off, 1, 2, Avg3(K, L, L));
            S(d, off, 3, 2, L); S(d, off, 2, 2, L);
            S(d, off, 0, 3, L); S(d, off, 1, 3, L); S(d, off, 2, 3, L); S(d, off, 3, 3, L);
        }

        private static void HD4(byte[] d, int off)
        {
            int I = D(d, off, -1, 0), J = D(d, off, -1, 1), K = D(d, off, -1, 2), L = D(d, off, -1, 3);
            int X = D(d, off, -1, -1), A = D(d, off, 0, -1), B = D(d, off, 1, -1), C = D(d, off, 2, -1);
            S(d, off, 0, 0, Avg2(I, X)); S(d, off, 2, 1, Avg2(I, X));
            S(d, off, 0, 1, Avg2(J, I)); S(d, off, 2, 2, Avg2(J, I));
            S(d, off, 0, 2, Avg2(K, J)); S(d, off, 2, 3, Avg2(K, J));
            S(d, off, 0, 3, Avg2(L, K));
            S(d, off, 3, 0, Avg3(A, B, C));
            S(d, off, 2, 0, Avg3(X, A, B));
            S(d, off, 1, 0, Avg3(I, X, A)); S(d, off, 3, 1, Avg3(I, X, A));
            S(d, off, 1, 1, Avg3(J, I, X)); S(d, off, 3, 2, Avg3(J, I, X));
            S(d, off, 1, 2, Avg3(K, J, I)); S(d, off, 3, 3, Avg3(K, J, I));
            S(d, off, 1, 3, Avg3(L, K, J));
        }

        // ====================================================================
        // In-loop filters

        private static void DoFilter2(byte[] p, int i, int step)
        {
            int p1 = p[i - 2 * step], p0 = p[i - step], q0 = p[i], q1 = p[i + step];
            int a = 3 * (q0 - p0) + Sclip1(p1 - q1);
            int a1 = Sclip2((a + 4) >> 3);
            int a2 = Sclip2((a + 3) >> 3);
            p[i - step] = Clip1(p0 + a2);
            p[i] = Clip1(q0 - a1);
        }

        private static void DoFilter4(byte[] p, int i, int step)
        {
            int p1 = p[i - 2 * step], p0 = p[i - step], q0 = p[i], q1 = p[i + step];
            int a = 3 * (q0 - p0);
            int a1 = Sclip2((a + 4) >> 3);
            int a2 = Sclip2((a + 3) >> 3);
            int a3 = (a1 + 1) >> 1;
            p[i - 2 * step] = Clip1(p1 + a3);
            p[i - step] = Clip1(p0 + a2);
            p[i] = Clip1(q0 - a1);
            p[i + step] = Clip1(q1 - a3);
        }

        private static void DoFilter6(byte[] p, int i, int step)
        {
            int p2 = p[i - 3 * step], p1 = p[i - 2 * step], p0 = p[i - step];
            int q0 = p[i], q1 = p[i + step], q2 = p[i + 2 * step];
            int a = Sclip1(3 * (q0 - p0) + Sclip1(p1 - q1));
            int a1 = (27 * a + 63) >> 7;
            int a2 = (18 * a + 63) >> 7;
            int a3 = (9 * a + 63) >> 7;
            p[i - 3 * step] = Clip1(p2 + a3);
            p[i - 2 * step] = Clip1(p1 + a2);
            p[i - step] = Clip1(p0 + a1);
            p[i] = Clip1(q0 - a1);
            p[i + step] = Clip1(q1 - a2);
            p[i + 2 * step] = Clip1(q2 - a3);
        }

        private static bool Hev(byte[] p, int i, int step, int thresh)
        {
            int p1 = p[i - 2 * step], p0 = p[i - step], q0 = p[i], q1 = p[i + step];
            return Abs0(p1 - p0) > thresh || Abs0(q1 - q0) > thresh;
        }

        private static bool NeedsFilter(byte[] p, int i, int step, int t)
        {
            int p1 = p[i - 2 * step], p0 = p[i - step], q0 = p[i], q1 = p[i + step];
            return (4 * Abs0(p0 - q0) + Abs0(p1 - q1)) <= t;
        }

        private static bool NeedsFilter2(byte[] p, int i, int step, int t, int it)
        {
            int p3 = p[i - 4 * step], p2 = p[i - 3 * step], p1 = p[i - 2 * step];
            int p0 = p[i - step], q0 = p[i];
            int q1 = p[i + step], q2 = p[i + 2 * step], q3 = p[i + 3 * step];
            if ((4 * Abs0(p0 - q0) + Abs0(p1 - q1)) > t) return false;
            return Abs0(p3 - p2) <= it && Abs0(p2 - p1) <= it &&
                   Abs0(p1 - p0) <= it && Abs0(q3 - q2) <= it &&
                   Abs0(q2 - q1) <= it && Abs0(q1 - q0) <= it;
        }

        // ---- simple filter (luma only) ----
        public static void SimpleVFilter16(byte[] p, int off, int stride, int thresh)
        {
            int t2 = 2 * thresh + 1;
            for (int i = 0; i < 16; ++i)
                if (NeedsFilter(p, off + i, stride, t2)) DoFilter2(p, off + i, stride);
        }

        public static void SimpleHFilter16(byte[] p, int off, int stride, int thresh)
        {
            int t2 = 2 * thresh + 1;
            for (int i = 0; i < 16; ++i)
                if (NeedsFilter(p, off + i * stride, 1, t2)) DoFilter2(p, off + i * stride, 1);
        }

        public static void SimpleVFilter16i(byte[] p, int off, int stride, int thresh)
        {
            for (int k = 3; k > 0; --k) { off += 4 * stride; SimpleVFilter16(p, off, stride, thresh); }
        }

        public static void SimpleHFilter16i(byte[] p, int off, int stride, int thresh)
        {
            for (int k = 3; k > 0; --k) { off += 4; SimpleHFilter16(p, off, stride, thresh); }
        }

        // ---- complex (normal) filter ----
        private static void FilterLoop26(byte[] p, int off, int hstride, int vstride,
                                         int size, int thresh, int ithresh, int hev)
        {
            int t2 = 2 * thresh + 1;
            while (size-- > 0)
            {
                if (NeedsFilter2(p, off, hstride, t2, ithresh))
                {
                    if (Hev(p, off, hstride, hev)) DoFilter2(p, off, hstride);
                    else DoFilter6(p, off, hstride);
                }
                off += vstride;
            }
        }

        private static void FilterLoop24(byte[] p, int off, int hstride, int vstride,
                                         int size, int thresh, int ithresh, int hev)
        {
            int t2 = 2 * thresh + 1;
            while (size-- > 0)
            {
                if (NeedsFilter2(p, off, hstride, t2, ithresh))
                {
                    if (Hev(p, off, hstride, hev)) DoFilter2(p, off, hstride);
                    else DoFilter4(p, off, hstride);
                }
                off += vstride;
            }
        }

        public static void VFilter16(byte[] p, int off, int stride, int t, int it, int hev)
            => FilterLoop26(p, off, stride, 1, 16, t, it, hev);

        public static void HFilter16(byte[] p, int off, int stride, int t, int it, int hev)
            => FilterLoop26(p, off, 1, stride, 16, t, it, hev);

        public static void VFilter16i(byte[] p, int off, int stride, int t, int it, int hev)
        {
            for (int k = 3; k > 0; --k) { off += 4 * stride; FilterLoop24(p, off, stride, 1, 16, t, it, hev); }
        }

        public static void HFilter16i(byte[] p, int off, int stride, int t, int it, int hev)
        {
            for (int k = 3; k > 0; --k) { off += 4; FilterLoop24(p, off, 1, stride, 16, t, it, hev); }
        }

        public static void VFilter8(byte[] u, int uo, byte[] v, int vo, int stride, int t, int it, int hev)
        {
            FilterLoop26(u, uo, stride, 1, 8, t, it, hev);
            FilterLoop26(v, vo, stride, 1, 8, t, it, hev);
        }

        public static void HFilter8(byte[] u, int uo, byte[] v, int vo, int stride, int t, int it, int hev)
        {
            FilterLoop26(u, uo, 1, stride, 8, t, it, hev);
            FilterLoop26(v, vo, 1, stride, 8, t, it, hev);
        }

        public static void VFilter8i(byte[] u, int uo, byte[] v, int vo, int stride, int t, int it, int hev)
        {
            FilterLoop24(u, uo + 4 * stride, stride, 1, 8, t, it, hev);
            FilterLoop24(v, vo + 4 * stride, stride, 1, 8, t, it, hev);
        }

        public static void HFilter8i(byte[] u, int uo, byte[] v, int vo, int stride, int t, int it, int hev)
        {
            FilterLoop24(u, uo + 4, 1, stride, 8, t, it, hev);
            FilterLoop24(v, vo + 4, 1, stride, 8, t, it, hev);
        }
    }
}
