using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace PocketTavern.UWP.Controls
{
    public sealed partial class MarkdownTextBlock : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarkdownTextBlock),
                new PropertyMetadata("", OnTextChanged));

        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register(nameof(TextFontSize), typeof(double), typeof(MarkdownTextBlock),
                new PropertyMetadata(14.0, OnTextChanged));

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

        public MarkdownTextBlock() { this.InitializeComponent(); }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((MarkdownTextBlock)d).Rebuild();

        private void Rebuild()
        {
            Rich.Blocks.Clear();
            var para = new Paragraph { FontSize = TextFontSize };

            foreach (var seg in ParseMarkdown(Text ?? ""))
            {
                Inline inline;

                if (seg.IsCode)
                {
                    var run = new Run
                    {
                        Text = seg.Text,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 230, 180))
                    };
                    inline = run;
                }
                else if (seg.IsQuote)
                {
                    var run = new Run
                    {
                        Text = seg.Text,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 233, 180, 86))
                    };
                    inline = run;
                }
                else if (seg.IsBold && seg.IsItalic)
                {
                    var span = new Span { FontWeight = FontWeights.Bold, FontStyle = FontStyle.Italic };
                    span.Inlines.Add(new Run { Text = seg.Text });
                    inline = span;
                }
                else if (seg.IsBold)
                {
                    var bold = new Bold();
                    bold.Inlines.Add(new Run { Text = seg.Text });
                    inline = bold;
                }
                else if (seg.IsItalic)
                {
                    var italic = new Italic();
                    italic.Inlines.Add(new Run { Text = seg.Text });
                    inline = italic;
                }
                else
                {
                    inline = new Run { Text = seg.Text };
                }

                para.Inlines.Add(inline);
            }

            Rich.Blocks.Add(para);
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
                    char after  = found + 1 < text.Length ? text[found + 1] : ' ';
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
