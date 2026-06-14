using System;
using System.Collections.Generic;

namespace PocketTavern.UWP.Services.WebP
{
    // Pure-managed VP8L (lossless WebP) decoder. Faithful port of libwebp's
    // reference decoder (src/dec/vp8l_dec.c + src/dsp/lossless.c, v1.3.2).
    // Decodes the whole image into an ARGB (0xAARRGGBB) buffer in one pass,
    // then applies the inverse transforms over the full image.
    internal sealed class Vp8LDecoder
    {
        private const int NumLiteralCodes = 256;
        private const int NumLengthCodes = 24;
        private const int NumDistanceCodes = 40;
        private const int NumCodeLengthCodes = 19;
        private const int CodesPerMetaCode = 5;
        private const int MaxCacheBits = 11;
        private const int DefaultCodeLength = 8;

        private const int GREEN = 0, RED = 1, BLUE = 2, ALPHA = 3, DIST = 4;
        private const uint ArgbBlack = 0xff000000u;
        private const uint HashMul = 0x1e35a7bdu;

        // Transform type ids (2-bit field in the stream).
        private const int PredictorTransform = 0;
        private const int CrossColorTransform = 1;
        private const int SubtractGreenTransform = 2;
        private const int ColorIndexingTransform = 3;

        private static readonly int[] kAlphabetSize =
        {
            NumLiteralCodes + NumLengthCodes, NumLiteralCodes,
            NumLiteralCodes, NumLiteralCodes, NumDistanceCodes
        };
        private static readonly int[] kCodeLengthCodeOrder =
            { 17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        private static readonly int[] kCodeLengthExtraBits = { 2, 3, 7 };
        private static readonly int[] kCodeLengthRepeatOffsets = { 3, 3, 11 };
        private static readonly byte[] kCodeToPlane =
        {
            0x18, 0x07, 0x17, 0x19, 0x28, 0x06, 0x27, 0x29, 0x16, 0x1a,
            0x26, 0x2a, 0x38, 0x05, 0x37, 0x39, 0x15, 0x1b, 0x36, 0x3a,
            0x25, 0x2b, 0x48, 0x04, 0x47, 0x49, 0x14, 0x1c, 0x35, 0x3b,
            0x46, 0x4a, 0x24, 0x2c, 0x58, 0x45, 0x4b, 0x34, 0x3c, 0x03,
            0x57, 0x59, 0x13, 0x1d, 0x56, 0x5a, 0x23, 0x2d, 0x44, 0x4c,
            0x55, 0x5b, 0x33, 0x3d, 0x68, 0x02, 0x67, 0x69, 0x12, 0x1e,
            0x66, 0x6a, 0x22, 0x2e, 0x54, 0x5c, 0x43, 0x4d, 0x65, 0x6b,
            0x32, 0x3e, 0x78, 0x01, 0x77, 0x79, 0x53, 0x5d, 0x11, 0x1f,
            0x64, 0x6c, 0x42, 0x4e, 0x76, 0x7a, 0x21, 0x2f, 0x75, 0x7b,
            0x31, 0x3f, 0x63, 0x6d, 0x52, 0x5e, 0x00, 0x74, 0x7c, 0x41,
            0x4f, 0x10, 0x20, 0x62, 0x6e, 0x30, 0x73, 0x7d, 0x51, 0x5f,
            0x40, 0x72, 0x7e, 0x61, 0x6f, 0x50, 0x71, 0x7f, 0x60, 0x70
        };

        private sealed class Transform
        {
            public int Type;
            public int Bits;
            public int Xsize;   // width before this transform was applied
            public int Ysize;
            public uint[] Data;
        }

        private sealed class HTreeGroup
        {
            public readonly HuffmanTree[] Trees = new HuffmanTree[CodesPerMetaCode];
        }

        private sealed class Meta
        {
            public uint[] HuffmanImage;   // null when there is a single htree group
            public int SubsampleBits;
            public int HuffmanXsize;
            public int Mask;
            public HTreeGroup[] Groups;
            public int ColorCacheBits;
            public int ColorCacheSize;
        }

        private Vp8LBitReader _br;
        private readonly List<Transform> _transforms = new List<Transform>();
        private uint _transformsSeen;

        // Entry point. Returns an ARGB buffer (length width*height) or null.
        public static uint[] Decode(byte[] data, int offset, int length,
                                    out int width, out int height)
        {
            return new Vp8LDecoder().Run(data, offset, length, out width, out height);
        }

        private uint[] Run(byte[] data, int offset, int length,
                           out int width, out int height)
        {
            width = height = 0;
            _br = new Vp8LBitReader(data, offset, length);

            if (_br.ReadBits(8) != 0x2f) return null;          // VP8L signature
            int w = (int)_br.ReadBits(14) + 1;
            int h = (int)_br.ReadBits(14) + 1;
            _br.ReadBits(1);                                    // alpha_is_used (informational)
            if (_br.ReadBits(3) != 0) return null;              // version must be 0
            if (_br.Eos) return null;

            uint[] argb = DecodeImageStream(w, h, true);
            if (argb == null) return null;

            width = w;
            height = h;
            return argb;
        }

        // Decodes one (possibly nested) entropy-coded image. For the top-level
        // image (isLevel0) it also reads/inverts the transforms.
        private uint[] DecodeImageStream(int xsize, int ysize, bool isLevel0)
        {
            int transformXsize = xsize;
            int transformYsize = ysize;
            int firstTransform = _transforms.Count;

            if (isLevel0)
            {
                while (_br.ReadBit() == 1)
                {
                    if (!ReadTransform(ref transformXsize, ref transformYsize)) return null;
                    if (_br.Eos) return null;
                }
            }

            int colorCacheBits = 0;
            if (_br.ReadBit() == 1)
            {
                colorCacheBits = (int)_br.ReadBits(4);
                if (colorCacheBits < 1 || colorCacheBits > MaxCacheBits) return null;
            }

            Meta meta = ReadHuffmanCodes(transformXsize, transformYsize,
                                         colorCacheBits, isLevel0);
            if (meta == null) return null;

            uint[] data = new uint[transformXsize * transformYsize];
            if (!DecodeImageData(data, transformXsize, transformYsize, meta)) return null;

            if (!isLevel0) return data;

            // Apply inverse transforms in reverse of the order they were read.
            uint[] cur = data;
            for (int i = _transforms.Count - 1; i >= firstTransform; i--)
            {
                cur = ApplyInverseTransform(_transforms[i], cur);
            }
            return cur;
        }

        // -------------------------------------------------------------------
        // Transforms

        private bool ReadTransform(ref int xsize, ref int ysize)
        {
            int type = (int)_br.ReadBits(2);
            if ((_transformsSeen & (1u << type)) != 0) return false;  // dup not allowed
            _transformsSeen |= (1u << type);

            var t = new Transform { Type = type, Xsize = xsize, Ysize = ysize };

            switch (type)
            {
                case PredictorTransform:
                case CrossColorTransform:
                    t.Bits = (int)_br.ReadBits(3) + 2;
                    t.Data = DecodeImageStream(SubSampleSize(t.Xsize, t.Bits),
                                               SubSampleSize(t.Ysize, t.Bits), false);
                    if (t.Data == null) return false;
                    break;

                case ColorIndexingTransform:
                {
                    int numColors = (int)_br.ReadBits(8) + 1;
                    int bits = (numColors > 16) ? 0
                             : (numColors > 4) ? 1
                             : (numColors > 2) ? 2
                             : 3;
                    xsize = SubSampleSize(t.Xsize, bits);
                    t.Bits = bits;
                    uint[] map = DecodeImageStream(numColors, 1, false);
                    if (map == null) return false;
                    t.Data = ExpandColorMap(numColors, bits, map);
                    break;
                }

                case SubtractGreenTransform:
                    break;

                default:
                    return false;
            }

            _transforms.Add(t);
            return true;
        }

        // Cumulative (delta) decode of the palette, then expand to the full
        // bundled size so packed indices always map to a valid entry.
        private static uint[] ExpandColorMap(int numColors, int bits, uint[] map)
        {
            int finalNumColors = 1 << (8 >> bits);
            var src = new byte[numColors * 4];
            for (int i = 0; i < numColors; i++)
            {
                uint c = map[i];
                src[i * 4 + 0] = (byte)(c & 0xff);
                src[i * 4 + 1] = (byte)((c >> 8) & 0xff);
                src[i * 4 + 2] = (byte)((c >> 16) & 0xff);
                src[i * 4 + 3] = (byte)((c >> 24) & 0xff);
            }
            var dst = new byte[finalNumColors * 4];
            dst[0] = src[0]; dst[1] = src[1]; dst[2] = src[2]; dst[3] = src[3];
            for (int i = 4; i < 4 * numColors; i++)
            {
                dst[i] = (byte)((src[i] + dst[i - 4]) & 0xff);
            }
            // Remaining entries stay zero (black tail).
            var outMap = new uint[finalNumColors];
            for (int i = 0; i < finalNumColors; i++)
            {
                outMap[i] = (uint)dst[i * 4]
                          | ((uint)dst[i * 4 + 1] << 8)
                          | ((uint)dst[i * 4 + 2] << 16)
                          | ((uint)dst[i * 4 + 3] << 24);
            }
            return outMap;
        }

        // -------------------------------------------------------------------
        // Huffman code reading

        private Meta ReadHuffmanCodes(int xsize, int ysize, int colorCacheBits,
                                      bool allowRecursion)
        {
            var meta = new Meta
            {
                ColorCacheBits = colorCacheBits,
                ColorCacheSize = colorCacheBits > 0 ? (1 << colorCacheBits) : 0
            };

            uint[] huffmanImage = null;
            int numHtreeGroups = 1;
            int subsampleBits = 0;
            int huffmanXsize = 0;

            if (allowRecursion && _br.ReadBit() == 1)
            {
                subsampleBits = (int)_br.ReadBits(3) + 2;
                huffmanXsize = SubSampleSize(xsize, subsampleBits);
                int huffmanYsize = SubSampleSize(ysize, subsampleBits);
                uint[] img = DecodeImageStream(huffmanXsize, huffmanYsize, false);
                if (img == null) return null;

                int pixs = huffmanXsize * huffmanYsize;
                int maxGroup = 1;
                for (int i = 0; i < pixs; i++)
                {
                    // Group index is stored in the red+green bytes.
                    int group = (int)((img[i] >> 8) & 0xffff);
                    img[i] = (uint)group;
                    if (group >= maxGroup) maxGroup = group + 1;
                }
                numHtreeGroups = maxGroup;
                huffmanImage = img;
            }
            if (_br.Eos) return null;

            meta.SubsampleBits = subsampleBits;
            meta.HuffmanXsize = huffmanXsize;
            meta.Mask = (subsampleBits == 0) ? ~0 : ((1 << subsampleBits) - 1);
            meta.HuffmanImage = huffmanImage;

            int maxAlphabet = kAlphabetSize[0] + meta.ColorCacheSize;
            var codeLengths = new int[maxAlphabet];

            var groups = new HTreeGroup[numHtreeGroups];
            for (int g = 0; g < numHtreeGroups; g++)
            {
                var grp = new HTreeGroup();
                for (int j = 0; j < CodesPerMetaCode; j++)
                {
                    int alphabetSize = kAlphabetSize[j];
                    if (j == 0 && colorCacheBits > 0) alphabetSize += meta.ColorCacheSize;
                    HuffmanTree tree = ReadHuffmanCode(alphabetSize, codeLengths);
                    if (tree == null) return null;
                    grp.Trees[j] = tree;
                }
                groups[g] = grp;
            }
            meta.Groups = groups;
            return meta;
        }

        private HuffmanTree ReadHuffmanCode(int alphabetSize, int[] codeLengths)
        {
            Array.Clear(codeLengths, 0, alphabetSize);

            if (_br.ReadBit() == 1)   // simple code length code
            {
                int numSymbols = (int)_br.ReadBits(1) + 1;
                int firstLenCode = (int)_br.ReadBits(1);
                int symbol = (int)_br.ReadBits(firstLenCode == 0 ? 1 : 8);
                if (symbol >= alphabetSize) return null;
                codeLengths[symbol] = 1;
                if (numSymbols == 2)
                {
                    symbol = (int)_br.ReadBits(8);
                    if (symbol >= alphabetSize) return null;
                    codeLengths[symbol] = 1;
                }
            }
            else                       // full Huffman-coded code lengths
            {
                var clcl = new int[NumCodeLengthCodes];
                int numCodes = (int)_br.ReadBits(4) + 4;
                if (numCodes > NumCodeLengthCodes) return null;
                for (int i = 0; i < numCodes; i++)
                {
                    clcl[kCodeLengthCodeOrder[i]] = (int)_br.ReadBits(3);
                }
                if (!ReadHuffmanCodeLengths(clcl, alphabetSize, codeLengths)) return null;
            }

            if (_br.Eos) return null;
            return HuffmanTree.Build(codeLengths, alphabetSize);
        }

        private bool ReadHuffmanCodeLengths(int[] clcl, int numSymbols, int[] codeLengths)
        {
            HuffmanTree lenTree = HuffmanTree.Build(clcl, NumCodeLengthCodes);
            if (lenTree == null) return false;

            int maxSymbol;
            int prevCodeLen = DefaultCodeLength;

            if (_br.ReadBit() == 1)
            {
                int lengthNbits = 2 + 2 * (int)_br.ReadBits(3);
                maxSymbol = 2 + (int)_br.ReadBits(lengthNbits);
                if (maxSymbol > numSymbols) return false;
            }
            else
            {
                maxSymbol = numSymbols;
            }

            int symbol = 0;
            while (symbol < numSymbols)
            {
                if (maxSymbol-- == 0) break;
                if (_br.Eos) return false;
                int codeLen = lenTree.ReadSymbol(_br);
                if (codeLen < 16)
                {
                    codeLengths[symbol++] = codeLen;
                    if (codeLen != 0) prevCodeLen = codeLen;
                }
                else
                {
                    bool usePrev = (codeLen == 16);
                    int slot = codeLen - 16;
                    if (slot < 0 || slot >= 3) return false;
                    int repeat = (int)_br.ReadBits(kCodeLengthExtraBits[slot])
                               + kCodeLengthRepeatOffsets[slot];
                    if (symbol + repeat > numSymbols) return false;
                    int len = usePrev ? prevCodeLen : 0;
                    while (repeat-- > 0) codeLengths[symbol++] = len;
                }
            }
            return true;
        }

        // -------------------------------------------------------------------
        // Entropy-coded pixel data (LZ77 + color cache)

        private bool DecodeImageData(uint[] data, int width, int height, Meta meta)
        {
            int total = width * height;
            int pos = 0, col = 0, row = 0;
            int lenCodeLimit = NumLiteralCodes + NumLengthCodes;
            int colorCacheLimit = lenCodeLimit + meta.ColorCacheSize;
            int mask = meta.Mask;

            uint[] cache = meta.ColorCacheSize > 0 ? new uint[meta.ColorCacheSize] : null;
            int cacheShift = 32 - meta.ColorCacheBits;
            int lastCached = 0;

            HTreeGroup grp = GetHtreeGroup(meta, col, row);

            while (pos < total)
            {
                if ((col & mask) == 0) grp = GetHtreeGroup(meta, col, row);

                int code = grp.Trees[GREEN].ReadSymbol(_br);
                if (_br.Eos) break;

                if (code < NumLiteralCodes)            // literal pixel
                {
                    int green = code;
                    int red = grp.Trees[RED].ReadSymbol(_br);
                    int blue = grp.Trees[BLUE].ReadSymbol(_br);
                    int alpha = grp.Trees[ALPHA].ReadSymbol(_br);
                    if (_br.Eos) break;
                    data[pos] = ((uint)alpha << 24) | ((uint)red << 16)
                              | ((uint)green << 8) | (uint)blue;
                    pos++;
                    if (++col >= width) { col = 0; row++; }
                }
                else if (code < lenCodeLimit)          // backward reference
                {
                    int length = GetCopyLength(code - NumLiteralCodes);
                    int distSym = grp.Trees[DIST].ReadSymbol(_br);
                    int distCode = GetCopyDistance(distSym);
                    int dist = PlaneCodeToDistance(width, distCode);
                    if (_br.Eos) break;
                    if (pos < dist || total - pos < length) return false;

                    int srcIdx = pos - dist;
                    for (int i = 0; i < length; i++) data[pos + i] = data[srcIdx + i];
                    pos += length;
                    col += length;
                    while (col >= width) { col -= width; row++; }
                    // A multi-pixel copy can land mid-tile in a different meta-
                    // Huffman group; the top-of-loop (col & mask)==0 check would
                    // miss that, so refetch here (matches libwebp).
                    if ((col & mask) != 0) grp = GetHtreeGroup(meta, col, row);
                }
                else if (code < colorCacheLimit)       // color cache reference
                {
                    if (cache != null)
                        while (lastCached < pos) CacheInsert(cache, cacheShift, data[lastCached++]);
                    data[pos] = cache[code - lenCodeLimit];
                    pos++;
                    if (++col >= width) { col = 0; row++; }
                }
                else
                {
                    return false;
                }

                if (cache != null)
                    while (lastCached < pos) CacheInsert(cache, cacheShift, data[lastCached++]);
            }

            return pos == total;
        }

        private static void CacheInsert(uint[] cache, int shift, uint argb)
        {
            cache[(argb * HashMul) >> shift] = argb;
        }

        private static HTreeGroup GetHtreeGroup(Meta meta, int col, int row)
        {
            if (meta.HuffmanImage == null) return meta.Groups[0];
            int index = (row >> meta.SubsampleBits) * meta.HuffmanXsize
                      + (col >> meta.SubsampleBits);
            int g = (int)meta.HuffmanImage[index];
            if (g < 0 || g >= meta.Groups.Length) g = 0;
            return meta.Groups[g];
        }

        private int GetCopyLength(int sym) => GetCopyDistance(sym);

        private int GetCopyDistance(int sym)
        {
            if (sym < 4) return sym + 1;
            int extraBits = (sym - 2) >> 1;
            int offset = (2 + (sym & 1)) << extraBits;
            return offset + (int)_br.ReadBits(extraBits) + 1;
        }

        private static int PlaneCodeToDistance(int xsize, int planeCode)
        {
            if (planeCode > 120) return planeCode - 120;
            int distCode = kCodeToPlane[planeCode - 1];
            int yoffset = distCode >> 4;
            int xoffset = 8 - (distCode & 0xf);
            int dist = yoffset * xsize + xoffset;
            return dist >= 1 ? dist : 1;
        }

        // -------------------------------------------------------------------
        // Inverse transforms (operate over the full image)

        private static uint[] ApplyInverseTransform(Transform t, uint[] inp)
        {
            switch (t.Type)
            {
                case SubtractGreenTransform: return InverseSubtractGreen(t, inp);
                case PredictorTransform:     return InversePredictor(t, inp);
                case CrossColorTransform:    return InverseCrossColor(t, inp);
                case ColorIndexingTransform: return InverseColorIndex(t, inp);
                default:                     return inp;
            }
        }

        private static uint[] InverseSubtractGreen(Transform t, uint[] inp)
        {
            int n = t.Xsize * t.Ysize;
            var outp = new uint[n];
            for (int i = 0; i < n; i++)
            {
                uint argb = inp[i];
                uint green = (argb >> 8) & 0xff;
                uint redBlue = argb & 0x00ff00ffu;
                redBlue += (green << 16) | green;
                redBlue &= 0x00ff00ffu;
                outp[i] = (argb & 0xff00ff00u) | redBlue;
            }
            return outp;
        }

        private static uint[] InversePredictor(Transform t, uint[] inp)
        {
            int width = t.Xsize, height = t.Ysize;
            var outp = new uint[width * height];

            // Row 0: first pixel is black-predicted, rest follow the left pixel.
            outp[0] = AddPixels(inp[0], ArgbBlack);
            for (int x = 1; x < width; x++)
                outp[x] = AddPixels(inp[x], outp[x - 1]);

            int tileWidth = 1 << t.Bits;
            int tileMask = tileWidth - 1;
            int tilesPerRow = SubSampleSize(width, t.Bits);

            for (int y = 1; y < height; y++)
            {
                int predModeRow = (y >> t.Bits) * tilesPerRow;
                int rowStart = y * width;
                // First pixel of each row follows the pixel above (mode 2).
                outp[rowStart] = AddPixels(inp[rowStart], outp[rowStart - width]);

                int x = 1;
                while (x < width)
                {
                    int mode = (int)((t.Data[predModeRow + (x >> t.Bits)] >> 8) & 0xf);
                    int xEnd = (x & ~tileMask) + tileWidth;
                    if (xEnd > width) xEnd = width;
                    for (; x < xEnd; x++)
                    {
                        int gi = rowStart + x;
                        uint pred = Predict(mode, outp, gi, width);
                        outp[gi] = AddPixels(inp[gi], pred);
                    }
                }
            }
            return outp;
        }

        private static uint Predict(int mode, uint[] o, int gi, int width)
        {
            uint left = o[gi - 1];
            uint top = o[gi - width];
            uint topLeft = o[gi - width - 1];
            uint topRight = o[gi - width + 1];
            switch (mode)
            {
                case 0:  return ArgbBlack;
                case 1:  return left;
                case 2:  return top;
                case 3:  return topRight;
                case 4:  return topLeft;
                case 5:  return Average3(left, top, topRight);
                case 6:  return Average2(left, topLeft);
                case 7:  return Average2(left, top);
                case 8:  return Average2(topLeft, top);
                case 9:  return Average2(top, topRight);
                case 10: return Average4(left, topLeft, top, topRight);
                case 11: return Select(top, left, topLeft);
                case 12: return ClampedAddSubtractFull(left, top, topLeft);
                case 13: return ClampedAddSubtractHalf(left, top, topLeft);
                default: return ArgbBlack;
            }
        }

        private static uint[] InverseCrossColor(Transform t, uint[] inp)
        {
            int width = t.Xsize, height = t.Ysize;
            var outp = new uint[width * height];
            int tilesPerRow = SubSampleSize(width, t.Bits);

            for (int y = 0; y < height; y++)
            {
                int predRow = (y >> t.Bits) * tilesPerRow;
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    uint code = t.Data[predRow + (x >> t.Bits)];
                    sbyte g2r = (sbyte)(code & 0xff);
                    sbyte g2b = (sbyte)((code >> 8) & 0xff);
                    sbyte r2b = (sbyte)((code >> 16) & 0xff);

                    uint argb = inp[rowStart + x];
                    sbyte green = (sbyte)(argb >> 8);
                    int newRed = (int)((argb >> 16) & 0xff);
                    int newBlue = (int)(argb & 0xff);
                    newRed += ColorTransformDelta(g2r, green);
                    newRed &= 0xff;
                    newBlue += ColorTransformDelta(g2b, green);
                    newBlue += ColorTransformDelta(r2b, (sbyte)newRed);
                    newBlue &= 0xff;
                    outp[rowStart + x] = (argb & 0xff00ff00u)
                                       | ((uint)newRed << 16) | (uint)newBlue;
                }
            }
            return outp;
        }

        private static int ColorTransformDelta(sbyte pred, sbyte color)
            => (pred * color) >> 5;

        private static uint[] InverseColorIndex(Transform t, uint[] inp)
        {
            int width = t.Xsize, height = t.Ysize;
            var outp = new uint[width * height];
            uint[] map = t.Data;
            int mapLen = map.Length;

            if (t.Bits > 0)   // packed pixel bundling
            {
                int bitsPerPixel = 8 >> t.Bits;
                int pixelsPerByte = 1 << t.Bits;
                int countMask = pixelsPerByte - 1;
                uint bitMask = (uint)((1 << bitsPerPixel) - 1);
                int inW = SubSampleSize(width, t.Bits);

                for (int y = 0; y < height; y++)
                {
                    int si = y * inW;
                    uint packed = 0;
                    int dstRow = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        if ((x & countMask) == 0) packed = (inp[si++] >> 8) & 0xff;
                        int idx = (int)(packed & bitMask);
                        outp[dstRow + x] = idx < mapLen ? map[idx] : 0u;
                        packed >>= bitsPerPixel;
                    }
                }
            }
            else              // one index per pixel (green channel)
            {
                int n = width * height;
                for (int i = 0; i < n; i++)
                {
                    int idx = (int)((inp[i] >> 8) & 0xff);
                    outp[i] = idx < mapLen ? map[idx] : 0u;
                }
            }
            return outp;
        }

        // -------------------------------------------------------------------
        // Pixel math helpers (bit-exact copies of libwebp's lossless.c)

        private static uint AddPixels(uint a, uint b)
        {
            uint alphaGreen = (a & 0xff00ff00u) + (b & 0xff00ff00u);
            uint redBlue = (a & 0x00ff00ffu) + (b & 0x00ff00ffu);
            return (alphaGreen & 0xff00ff00u) | (redBlue & 0x00ff00ffu);
        }

        private static uint Average2(uint a0, uint a1)
            => (((a0 ^ a1) & 0xfefefefeu) >> 1) + (a0 & a1);

        private static uint Average3(uint a0, uint a1, uint a2)
            => Average2(Average2(a0, a2), a1);

        private static uint Average4(uint a0, uint a1, uint a2, uint a3)
            => Average2(Average2(a0, a1), Average2(a2, a3));

        private static uint Clip255(uint a)
        {
            if (a < 256) return a;
            return ~a >> 24;   // 0 if negative-as-unsigned, 255 if overflow
        }

        private static int AddSubtractComponentFull(int a, int b, int c)
            => (int)Clip255((uint)(a + b - c));

        private static uint ClampedAddSubtractFull(uint c0, uint c1, uint c2)
        {
            int a = AddSubtractComponentFull((int)(c0 >> 24), (int)(c1 >> 24), (int)(c2 >> 24));
            int r = AddSubtractComponentFull((int)((c0 >> 16) & 0xff), (int)((c1 >> 16) & 0xff), (int)((c2 >> 16) & 0xff));
            int g = AddSubtractComponentFull((int)((c0 >> 8) & 0xff), (int)((c1 >> 8) & 0xff), (int)((c2 >> 8) & 0xff));
            int b = AddSubtractComponentFull((int)(c0 & 0xff), (int)(c1 & 0xff), (int)(c2 & 0xff));
            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }

        private static int AddSubtractComponentHalf(int a, int b)
            => (int)Clip255((uint)(a + (a - b) / 2));

        private static uint ClampedAddSubtractHalf(uint c0, uint c1, uint c2)
        {
            uint ave = Average2(c0, c1);
            int a = AddSubtractComponentHalf((int)(ave >> 24), (int)(c2 >> 24));
            int r = AddSubtractComponentHalf((int)((ave >> 16) & 0xff), (int)((c2 >> 16) & 0xff));
            int g = AddSubtractComponentHalf((int)((ave >> 8) & 0xff), (int)((c2 >> 8) & 0xff));
            int b = AddSubtractComponentHalf((int)(ave & 0xff), (int)(c2 & 0xff));
            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }

        private static int Sub3(int a, int b, int c)
        {
            int pb = Math.Abs(b - c);
            int pa = Math.Abs(a - c);
            return pb - pa;
        }

        private static uint Select(uint a, uint b, uint c)
        {
            int paMinusPb =
                Sub3((int)(a >> 24), (int)(b >> 24), (int)(c >> 24)) +
                Sub3((int)((a >> 16) & 0xff), (int)((b >> 16) & 0xff), (int)((c >> 16) & 0xff)) +
                Sub3((int)((a >> 8) & 0xff), (int)((b >> 8) & 0xff), (int)((c >> 8) & 0xff)) +
                Sub3((int)(a & 0xff), (int)(b & 0xff), (int)(c & 0xff));
            return (paMinusPb <= 0) ? a : b;
        }

        private static int SubSampleSize(int size, int samplingBits)
            => (size + (1 << samplingBits) - 1) >> samplingBits;
    }
}
