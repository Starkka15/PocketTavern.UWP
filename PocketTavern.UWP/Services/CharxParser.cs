using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PocketTavern.UWP.Services
{
    public class CharxResult
    {
        public string CardJson { get; set; }
        public byte[] IconPng { get; set; }
        public Dictionary<string, byte[]> Sprites { get; set; } = new Dictionary<string, byte[]>();
    }

    public static class CharxParser
    {
        private static readonly byte[] PkMagic = { 0x50, 0x4B, 0x03, 0x04 };

        /// <summary>
        /// Parses a .charx file (JPEG prepended to ZIP).
        /// Finds ZIP by scanning for PK\x03\x04 magic — never assumes offset 0. (V1)
        /// Returns card JSON, icon PNG bytes, and sprite dict keyed by sprite name.
        /// </summary>
        public static CharxResult Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
                throw new InvalidDataException("charx: file too small");

            int zipOffset = FindZipOffset(bytes);
            if (zipOffset < 0)
                throw new InvalidDataException("charx: ZIP signature PK\\x03\\x04 not found");

            var zipBytes = new byte[bytes.Length - zipOffset];
            Array.Copy(bytes, zipOffset, zipBytes, 0, zipBytes.Length);

            var result = new CharxResult();

            using (var ms = new MemoryStream(zipBytes))
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    System.Diagnostics.Debug.WriteLine($"[CharxParser] entry: {name} ({entry.Length} bytes)");

                    if (string.Equals(name, "card.json", StringComparison.OrdinalIgnoreCase))
                    {
                        if (entry.Length > 64L * 1024 * 1024)
                            throw new InvalidDataException("charx: card.json exceeds 64 MB limit");
                        using (var s = entry.Open())
                        using (var sr = new StreamReader(s, Encoding.UTF8))
                            result.CardJson = sr.ReadToEnd();
                        continue;
                    }

                    if (name.StartsWith("assets/icon/image/", StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        result.IconPng = ReadEntry(entry);
                        System.Diagnostics.Debug.WriteLine($"[CharxParser] icon found: {name}");
                        continue;
                    }

                    if (name.StartsWith("assets/other/image/", StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        var spriteName = Path.GetFileNameWithoutExtension(name);
                        result.Sprites[spriteName] = ReadEntry(entry);
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CharxParser] done — icon={result.IconPng != null}, sprites={result.Sprites.Count}");

            if (string.IsNullOrWhiteSpace(result.CardJson))
                throw new InvalidDataException("charx: card.json not found in archive");

            return result;
        }

        private static int FindZipOffset(byte[] data)
        {
            for (int i = 0; i <= data.Length - 4; i++)
            {
                if (data[i] == PkMagic[0] && data[i + 1] == PkMagic[1]
                    && data[i + 2] == PkMagic[2] && data[i + 3] == PkMagic[3])
                    return i;
            }
            return -1;
        }

        private static byte[] ReadEntry(ZipArchiveEntry entry)
        {
            if (entry.Length > 64L * 1024 * 1024)
                throw new InvalidDataException($"charx: entry '{entry.Name}' exceeds 64 MB limit");
            using (var s = entry.Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
