using System;
using System.Threading.Tasks;

namespace PocketTavern.UWP.Services
{
    public static class ImageConversionService
    {
        private static readonly byte[] RiffHeader = { 0x52, 0x49, 0x46, 0x46 };
        private static readonly byte[] WebpMagic = { 0x57, 0x45, 0x42, 0x50 };

        public static bool IsWebP(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12) return false;
            for (int i = 0; i < 4; i++)
                if (bytes[i] != RiffHeader[i]) return false;
            for (int i = 0; i < 4; i++)
                if (bytes[8 + i] != WebpMagic[i]) return false;
            return true;
        }

        public static async Task<byte[]> DecodeWebPToPngAsync(byte[] webpBytes)
        {
            try
            {
                return await WebPDecoder.DecodeToPngAsync(webpBytes);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[WebP] Decode failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}
