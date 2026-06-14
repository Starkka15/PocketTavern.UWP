using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using PocketTavern.UWP.Services.WebP;

namespace PocketTavern.UWP.Services
{
    // WebP decoder front-end. Parses the RIFF container, dispatches to the
    // pure-managed codec decoders, and re-encodes the result as PNG bytes
    // (via the always-available WIC PNG encoder) for BitmapImage consumption.
    //
    // The WebP decode itself is fully managed — no WIC WebP codec (absent on
    // Windows 10 Mobile) and no third-party native libraries.
    public static class WebPDecoder
    {
        private static readonly byte[] RiffH = { 0x52, 0x49, 0x46, 0x46 };  // "RIFF"
        private static readonly byte[] WebpM = { 0x57, 0x45, 0x42, 0x50 };  // "WEBP"

        public static bool IsWebP(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12) return false;
            for (int i = 0; i < 4; i++)
                if (bytes[i] != RiffH[i]) return false;
            for (int i = 0; i < 4; i++)
                if (bytes[8 + i] != WebpM[i]) return false;
            return true;
        }

        public static async Task<byte[]> DecodeToPngAsync(byte[] webpBytes)
        {
            int w, h;
            uint[] argb = DecodeToArgb(webpBytes, out w, out h);
            if (argb == null || w <= 0 || h <= 0) return null;

            // ARGB (0xAARRGGBB) -> RGBA byte order expected by Rgba8.
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                uint p = argb[i];
                int o = i * 4;
                rgba[o]     = (byte)((p >> 16) & 0xff); // R
                rgba[o + 1] = (byte)((p >> 8) & 0xff);  // G
                rgba[o + 2] = (byte)(p & 0xff);         // B
                rgba[o + 3] = (byte)((p >> 24) & 0xff); // A
            }

            using (var mem = new InMemoryRandomAccessStream())
            {
                var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, mem);
                enc.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight,
                                 (uint)w, (uint)h, 96, 96, rgba);
                await enc.FlushAsync();

                var result = new byte[(int)mem.Size];
                using (var rdr = new DataReader(mem.GetInputStreamAt(0)))
                {
                    await rdr.LoadAsync((uint)mem.Size);  // required before ReadBytes
                    rdr.ReadBytes(result);
                }
                return result;
            }
        }

        // Parses the container and decodes the embedded codec chunk to ARGB.
        // Returns null on failure (including not-yet-supported VP8 lossy).
        public static uint[] DecodeToArgb(byte[] data, out int width, out int height)
        {
            width = height = 0;
            if (!IsWebP(data)) return null;

            int riffEnd = Math.Min(data.Length, 8 + (int)ReadU32(data, 4));
            int off = 12;

            int codecOff = -1, codecLen = 0;
            bool isLossless = false;

            while (off + 8 <= riffEnd)
            {
                string fourcc = Fourcc(data, off);
                int sz = (int)ReadU32(data, off + 4);
                int payload = off + 8;
                if (payload + sz > riffEnd) sz = riffEnd - payload;

                if (fourcc == "VP8L")
                {
                    codecOff = payload; codecLen = sz; isLossless = true;
                }
                else if (fourcc == "VP8 ")
                {
                    codecOff = payload; codecLen = sz; isLossless = false;
                }
                else if (fourcc == "ANMF")
                {
                    // Animated WebP: decode the first frame's image sub-chunk.
                    int fp = payload + 16;          // skip 16-byte frame header
                    int fend = payload + sz;
                    while (fp + 8 <= fend)
                    {
                        string fcc = Fourcc(data, fp);
                        int fsz = (int)ReadU32(data, fp + 4);
                        int fpay = fp + 8;
                        if (fcc == "VP8L") { codecOff = fpay; codecLen = fsz; isLossless = true; }
                        else if (fcc == "VP8 ") { codecOff = fpay; codecLen = fsz; isLossless = false; }
                        fp = fpay + fsz + (fsz & 1);
                    }
                    break;  // only the first frame
                }
                // VP8X / ICCP / ALPH / EXIF / XMP are skipped for now.

                off = payload + sz + (sz & 1);  // chunks are padded to even size
            }

            if (codecOff < 0)
            {
                DebugLogger.Log("[WebP] No VP8/VP8L chunk found");
                return null;
            }

            if (isLossless)
            {
                return Vp8LDecoder.Decode(data, codecOff, codecLen, out width, out height);
            }

            return Vp8Decoder.Decode(data, codecOff, codecLen, out width, out height);
        }

        private static uint ReadU32(byte[] d, int off)
            => (uint)(d[off] | (d[off + 1] << 8) | (d[off + 2] << 16) | (d[off + 3] << 24));

        private static string Fourcc(byte[] d, int off)
            => "" + (char)d[off] + (char)d[off + 1] + (char)d[off + 2] + (char)d[off + 3];
    }
}
