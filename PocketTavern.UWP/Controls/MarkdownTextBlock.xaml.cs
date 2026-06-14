using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.Controls
{
    public sealed partial class MarkdownTextBlock : UserControl
    {
        private class MatchEntry
        {
            public int Start { get; set; }
            public int End { get; set; }
            public MessageChunk Chunk { get; set; }
        }

        private static readonly Regex SpriteTagRegex = new Regex(
            @"<\s*img\s+cmd=""([^""]+)""[^>]*>" +
            @"|<\s*img\s+cmd=''([^']+)''[^>]*>" +
            @"|<\s*img\s+cmd=<<([^>]+)>>[^>]*>" +
            @"|<\s*img\s+cmd=\(([^)]+)\)[^>]*>" +
            @"|<\s*img\s+src=\(([^)]+)\)\s*>" +
            @"|\bimg\s+src=\(([^)]+)\)" +
            @"|<\s*img\s*=\s*""([^""]+)""\s*/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MdImageRegex = new Regex(
            @"!\[([^\]]*)\][({]([^)}\s]+)[})]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HtmlImgRegex = new Regex(
            @"<\s*img[^>]*\bsrc=[""']?(https?://[^""'\s>]+)[""']?[^>]*>" +
            @"|<\s*img\s*=\s*[""']?(https?://[^""'\s>]+)[""']?\s*/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "PocketTavern/1.0" } }
        };

        private static readonly ConcurrentDictionary<string, byte[]> _imageCache =
            new ConcurrentDictionary<string, byte[]>();

        private CancellationTokenSource _cts;

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarkdownTextBlock),
                new PropertyMetadata("", OnTextChanged));

        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register(nameof(TextFontSize), typeof(double), typeof(MarkdownTextBlock),
                new PropertyMetadata(14.0, OnTextChanged));

        public static readonly DependencyProperty MaxImageWidthProperty =
            DependencyProperty.Register(nameof(MaxImageWidth), typeof(double), typeof(MarkdownTextBlock),
                new PropertyMetadata(260.0, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }

        public double MaxImageWidth
        {
            get => (double)GetValue(MaxImageWidthProperty);
            set => SetValue(MaxImageWidthProperty, value);
        }

        public MarkdownTextBlock() { this.InitializeComponent(); }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((MarkdownTextBlock)d).Rebuild();

        private void Rebuild()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            ContentPanel.Children.Clear();

            foreach (var chunk in SplitIntoChunks(Text ?? ""))
            {
                if (chunk is TextChunk tc)
                {
                    var tb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = TextFontSize,
                        Margin = new Thickness()
                    };
                    foreach (var seg in ParseMarkdown(tc.Text))
                    {
                        tb.Inlines.Add(BuildInline(seg));
                    }
                    ContentPanel.Children.Add(tb);
                }
                else if (chunk is ImageChunk ic)
                {
                    var image = new Image
                    {
                        MaxWidth = MaxImageWidth,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    ContentPanel.Children.Add(image);
                    LoadImageAsync(image, ic.Url, ic.Alt, token);
                }
                else if (chunk is Base64ImageChunk b64)
                {
                    var image = new Image
                    {
                        MaxWidth = MaxImageWidth,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    ContentPanel.Children.Add(image);
                    LoadBase64ImageAsync(image, b64.Data, token);
                }
            }
        }

        private async void LoadImageAsync(Image image, string url, string alt, CancellationToken ct)
        {
            byte[] bytes;
            try
            {
                if (_imageCache.TryGetValue(url, out var cached))
                {
                    bytes = cached;
                }
                else
                {
                    using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct))
                    {
                        response.EnsureSuccessStatusCode();
                        bytes = await response.Content.ReadAsByteArrayAsync();
                    }
                    _imageCache.TryAdd(url, bytes);
                }
            }
            catch
            {
                return;
            }

            if (ct.IsCancellationRequested) return;

            // WebP is not natively supported by BitmapImage — decode via SkiaSharp (x86/x64/ARM/ARM64)
            if (ImageConversionService.IsWebP(bytes))
            {
                var pngBytes = await ImageConversionService.DecodeWebPToPngAsync(bytes);
                if (pngBytes != null && !ct.IsCancellationRequested)
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        using (var ms = new MemoryStream(pngBytes))
                        {
                            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                        }
                        if (ct.IsCancellationRequested) return;
                        image.Source = bmp;
                    }
                    catch { }
                }
                return;
            }

            try
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                }
                if (ct.IsCancellationRequested) return;
                image.Source = bmp;
            }
            catch
            {
                if (ct.IsCancellationRequested) return;
            }
        }

        private async void LoadBase64ImageAsync(Image image, string rawBase64, CancellationToken ct)
        {
            byte[] bytes;
            try
            {
                bytes = await Task.Run(() => Convert.FromBase64String(rawBase64));
            }
            catch
            {
                return;
            }

            if (ct.IsCancellationRequested) return;

            // Embedded WebP (data: URIs) isn't natively decodable by BitmapImage.
            if (ImageConversionService.IsWebP(bytes))
            {
                var pngBytes = await ImageConversionService.DecodeWebPToPngAsync(bytes);
                if (pngBytes == null || ct.IsCancellationRequested) return;
                bytes = pngBytes;
            }

            try
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                }
                if (ct.IsCancellationRequested) return;
                image.Source = bmp;
            }
            catch
            {
                if (ct.IsCancellationRequested) return;
            }
        }

        private static Inline BuildInline(MarkdownSegment seg)
        {
            if (seg.IsCode)
            {
                return new Run
                {
                    Text = seg.Text,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 230, 180))
                };
            }
            else if (seg.IsQuote)
            {
                return new Run
                {
                    Text = seg.Text,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 233, 180, 86))
                };
            }
            else if (seg.IsBold && seg.IsItalic)
            {
                var span = new Span { FontWeight = FontWeights.Bold, FontStyle = FontStyle.Italic };
                span.Inlines.Add(new Run { Text = seg.Text });
                return span;
            }
            else if (seg.IsBold)
            {
                var bold = new Bold();
                bold.Inlines.Add(new Run { Text = seg.Text });
                return bold;
            }
            else if (seg.IsItalic)
            {
                var italic = new Italic();
                italic.Inlines.Add(new Run { Text = seg.Text });
                return italic;
            }
            else
            {
                return new Run { Text = seg.Text };
            }
        }

        private List<MessageChunk> SplitIntoChunks(string text)
        {
            string cleaned = SpriteTagRegex.Replace(text ?? "", "");
            var matches = new List<MatchEntry>();

            foreach (Match m in MdImageRegex.Matches(cleaned))
            {
                string url = m.Groups[2].Value.Trim();
                string alt = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(url)) continue;

                if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    int commaIdx = url.IndexOf(',');
                    if (commaIdx >= 0)
                    {
                        matches.Add(new MatchEntry { Start = m.Index, End = m.Index + m.Length, Chunk = new Base64ImageChunk { Data = url.Substring(commaIdx + 1), Alt = alt } });
                    }
                }
                else
                {
                    matches.Add(new MatchEntry { Start = m.Index, End = m.Index + m.Length, Chunk = new ImageChunk { Url = url, Alt = alt } });
                }
            }

            foreach (Match m in HtmlImgRegex.Matches(cleaned))
            {
                string url = m.Groups[1].Value;
                if (string.IsNullOrEmpty(url)) url = m.Groups[2].Value;
                url = url.Trim();
                if (!string.IsNullOrEmpty(url) && !url.StartsWith("{{"))
                {
                    matches.Add(new MatchEntry { Start = m.Index, End = m.Index + m.Length, Chunk = new ImageChunk { Url = url, Alt = "" } });
                }
            }

            matches.Sort((a, b) => a.Start.CompareTo(b.Start));

            var chunks = new List<MessageChunk>();
            int lastEnd = 0;
            foreach (var entry in matches)
            {
                if (entry.Start < lastEnd) continue;
                string before = cleaned.Substring(lastEnd, entry.Start - lastEnd).Trim();
                if (before.Length > 0)
                    chunks.Add(new TextChunk { Text = before });
                chunks.Add(entry.Chunk);
                lastEnd = entry.End;
            }
            string after = cleaned.Substring(lastEnd).Trim();
            if (after.Length > 0)
                chunks.Add(new TextChunk { Text = after });

            if (chunks.Count == 0)
                chunks.Add(new TextChunk { Text = text });

            return chunks;
        }

        private static List<MarkdownSegment> ParseMarkdown(string text)
        {
            var segments = new List<MarkdownSegment>();
            int i = 0;
            var sb = new System.Text.StringBuilder();

            void FlushPlain()
            {
                if (sb.Length > 0)
                {
                    segments.Add(new MarkdownSegment(sb.ToString()));
                    sb.Clear();
                }
            }

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        FlushPlain();
                        segments.Add(new MarkdownSegment(text.Substring(i + 1, end - i - 1), isCode: true));
                        i = end + 1;
                    }
                    else { sb.Append(c); i++; }
                }
                else if (c == '*')
                {
                    int stars = 0;
                    int j = i;
                    while (j < text.Length && text[j] == '*') { stars++; j++; }

                    if (stars >= 3)
                    {
                        int close = text.IndexOf("***", j);
                        if (close > j)
                        {
                            FlushPlain();
                            segments.Add(new MarkdownSegment(text.Substring(j, close - j), isBold: true, isItalic: true));
                            i = close + 3;
                        }
                        else { sb.Append('*', stars); i = j; }
                    }
                    else if (stars == 2)
                    {
                        int close = FindClose(text, j, "**");
                        if (close > j)
                        {
                            FlushPlain();
                            segments.Add(new MarkdownSegment(text.Substring(j, close - j), isBold: true));
                            i = close + 2;
                        }
                        else { sb.Append("**"); i = j; }
                    }
                    else
                    {
                        int close = FindClose(text, j, "*");
                        if (close > j)
                        {
                            FlushPlain();
                            segments.Add(new MarkdownSegment(text.Substring(j, close - j), isItalic: true));
                            i = close + 1;
                        }
                        else { sb.Append('*'); i = j; }
                    }
                }
                else if (c == '_')
                {
                    int close = FindClose(text, i + 1, "_");
                    if (close > i + 1)
                    {
                        FlushPlain();
                        segments.Add(new MarkdownSegment(text.Substring(i + 1, close - i - 1), isItalic: true));
                        i = close + 1;
                    }
                    else { sb.Append(c); i++; }
                }
                else if (c == '"')
                {
                    int close = text.IndexOf('"', i + 1);
                    if (close > i)
                    {
                        FlushPlain();
                        segments.Add(new MarkdownSegment(text.Substring(i, close - i + 1), isQuote: true));
                        i = close + 1;
                    }
                    else { sb.Append(c); i++; }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            FlushPlain();
            return segments;
        }

        private static int FindClose(string text, int start, string pattern)
        {
            int idx = start;
            while (idx < text.Length)
            {
                int found = text.IndexOf(pattern, idx);
                if (found < 0) return -1;

                if (pattern == "*")
                {
                    char before = found > 0 ? text[found - 1] : ' ';
                    char after = found + 1 < text.Length ? text[found + 1] : ' ';
                    if (before != '*' && after != '*') return found;
                    idx = found + 1;
                }
                else if (pattern == "**")
                {
                    char after = found + 2 < text.Length ? text[found + 2] : ' ';
                    if (after != '*') return found;
                    idx = found + 2;
                }
                else return found;
            }
            return -1;
        }

        private abstract class MessageChunk { }

        private class TextChunk : MessageChunk
        {
            public string Text { get; set; }
        }

        private class ImageChunk : MessageChunk
        {
            public string Url { get; set; }
            public string Alt { get; set; }
        }

        private class Base64ImageChunk : MessageChunk
        {
            public string Data { get; set; }
            public string Alt { get; set; }
        }

        private struct MarkdownSegment
        {
            public string Text;
            public bool IsBold, IsItalic, IsCode, IsQuote;
            public MarkdownSegment(string text, bool isBold = false, bool isItalic = false,
                                   bool isCode = false, bool isQuote = false)
            {
                Text = text; IsBold = isBold; IsItalic = isItalic;
                IsCode = isCode; IsQuote = isQuote;
            }
        }
    }
}
