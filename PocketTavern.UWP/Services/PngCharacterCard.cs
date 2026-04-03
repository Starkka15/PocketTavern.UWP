using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// Embeds and extracts Character Card V2 data from PNG tEXt chunks.
    /// Full spec: https://github.com/malfoyslastname/character-card-spec-v2
    /// </summary>
    public static class PngCharacterCard
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include
        };

        // PNG signature: 8 bytes
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>
        /// Embed character data into PNG bytes by inserting a tEXt chunk after IHDR.
        /// </summary>
        public static byte[] EmbedCharacterData(byte[] pngBytes, CharacterCardV2 cardData)
        {
            var jsonStr = JsonConvert.SerializeObject(cardData, _jsonSettings);
            var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonStr));

            // tEXt chunk content: "chara" + null byte + base64 data
            const string keyword = "chara";
            var keywordBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(keyword);
            var base64Bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(base64Data);

            var textContent = new byte[keywordBytes.Length + 1 + base64Bytes.Length];
            Buffer.BlockCopy(keywordBytes, 0, textContent, 0, keywordBytes.Length);
            textContent[keywordBytes.Length] = 0; // null separator
            Buffer.BlockCopy(base64Bytes, 0, textContent, keywordBytes.Length + 1, base64Bytes.Length);

            var chunkType = Encoding.GetEncoding("ISO-8859-1").GetBytes("tEXt");
            var textChunk = BuildPngChunk(chunkType, textContent);

            // Find insert position: after PNG signature (8) + IHDR chunk (4 len + 4 type + data + 4 crc)
            var ihdrLength = ReadInt32BigEndian(pngBytes, 8);
            var insertPos = 8 + 4 + 4 + ihdrLength + 4;

            var result = new byte[pngBytes.Length + textChunk.Length];
            Buffer.BlockCopy(pngBytes, 0, result, 0, insertPos);
            Buffer.BlockCopy(textChunk, 0, result, insertPos, textChunk.Length);
            Buffer.BlockCopy(pngBytes, insertPos, result, insertPos + textChunk.Length, pngBytes.Length - insertPos);
            return result;
        }

        /// <summary>
        /// Extract character data from PNG bytes, returns null if no card found.
        /// </summary>
        public static CharacterCardV2 ExtractCharacterData(byte[] pngBytes)
        {
            try
            {
                if (pngBytes == null || pngBytes.Length < 8) return null;

                // Verify PNG signature
                for (int i = 0; i < PngSignature.Length; i++)
                    if (pngBytes[i] != PngSignature[i]) return null;

                // Search for "tEXtchara" marker
                var marker = Encoding.GetEncoding("ISO-8859-1").GetBytes("tEXtchara");
                int markerPos = -1;

                for (int i = 0; i <= pngBytes.Length - marker.Length; i++)
                {
                    bool found = true;
                    for (int j = 0; j < marker.Length; j++)
                    {
                        if (pngBytes[i + j] != marker[j]) { found = false; break; }
                    }
                    if (found) { markerPos = i; break; }
                }

                if (markerPos < 0) return null;

                // Chunk length is 4 bytes before "tEXt"
                var lengthPos = markerPos - 4;
                if (lengthPos < 0) return null;

                var length = ReadInt32BigEndian(pngBytes, lengthPos);

                // Base64 data starts after "tEXtchara\0" (10 bytes from marker)
                var dataStart = markerPos + 10;
                var dataLength = length - 6; // subtract "chara\0"

                if (dataStart + dataLength > pngBytes.Length) return null;

                var base64Data = Encoding.GetEncoding("ISO-8859-1").GetString(pngBytes, dataStart, dataLength);
                var jsonBytes = Convert.FromBase64String(base64Data);
                var jsonStr = Encoding.UTF8.GetString(jsonBytes);

                // Try V2 format first
                try
                {
                    var v2 = JsonConvert.DeserializeObject<CharacterCardV2>(jsonStr, _jsonSettings);
                    if (v2?.Data != null) return v2;
                }
                catch { }

                // Fall back to V1 (direct CharacterCardData without wrapper)
                try
                {
                    var v1Data = JsonConvert.DeserializeObject<CharacterCardData>(jsonStr, _jsonSettings);
                    if (v1Data != null)
                        return new CharacterCardV2 { Data = v1Data };
                }
                catch { }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Create a CharacterCardV2 from basic character info.</summary>
        public static CharacterCardV2 CreateCard(
            string name,
            string description = "",
            string personality = "",
            string scenario = "",
            string firstMessage = "",
            string messageExample = "")
        {
            return new CharacterCardV2
            {
                Data = new CharacterCardData
                {
                    Name = name,
                    Description = description,
                    Personality = personality,
                    Scenario = scenario,
                    FirstMes = firstMessage,
                    MesExample = messageExample
                }
            };
        }

        private static byte[] BuildPngChunk(byte[] type, byte[] data)
        {
            var length = ToBigEndianBytes(data.Length);

            // CRC is computed over type + data
            var crcInput = new byte[type.Length + data.Length];
            Buffer.BlockCopy(type, 0, crcInput, 0, type.Length);
            Buffer.BlockCopy(data, 0, crcInput, type.Length, data.Length);
            var crcValue = ComputeCrc32(crcInput);
            var crc = ToBigEndianBytes((int)crcValue);

            var chunk = new byte[4 + type.Length + data.Length + 4];
            Buffer.BlockCopy(length, 0, chunk, 0, 4);
            Buffer.BlockCopy(type, 0, chunk, 4, type.Length);
            Buffer.BlockCopy(data, 0, chunk, 4 + type.Length, data.Length);
            Buffer.BlockCopy(crc, 0, chunk, 4 + type.Length + data.Length, 4);
            return chunk;
        }

        private static int ReadInt32BigEndian(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24)
                 | (buffer[offset + 1] << 16)
                 | (buffer[offset + 2] << 8)
                 | buffer[offset + 3];
        }

        private static byte[] ToBigEndianBytes(int value)
        {
            return new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }

        private static readonly uint[] _crcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        private static uint ComputeCrc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
                crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }
    }
}
