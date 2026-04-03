using System;
using System.Text.RegularExpressions;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using PocketTavern.UWP.Controls;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class RegexSettingsPage : Page
    {
        private readonly RegexSettingsViewModel _vm = new RegexSettingsViewModel();

        private SolidColorBrush Accent    => (SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"];
        private SolidColorBrush TextPri   => (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
        private SolidColorBrush TextSec   => (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
        private SolidColorBrush CardBg    => (SolidColorBrush)Application.Current.Resources["BackgroundCardBrush"];
        private SolidColorBrush SurfaceBg => (SolidColorBrush)Application.Current.Resources["BackgroundSurfaceBrush"];
        private static SolidColorBrush ErrorBrush => new SolidColorBrush(Color.FromArgb(255, 207, 102, 121));

        public RegexSettingsPage() { this.InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.Load();
            Rebuild();
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnAddRuleClick(object sender, RoutedEventArgs e)
        {
            var rule = await ShowRuleDialog(null);
            if (rule == null) return;
            _vm.AddRule(rule.Name, rule.Pattern, rule.IsRegex, rule.Replacement,
                rule.ApplyToOutput, rule.ApplyToInput, rule.CaseInsensitive);
            Rebuild();
        }

        // ── Rebuild ──────────────────────────────────────────────────────────

        private void Rebuild()
        {
            RootPanel.Children.Clear();

            if (_vm.Rules.Count == 0)
            {
                RootPanel.Children.Add(BuildEmptyState());
                return;
            }

            foreach (var rule in _vm.Rules)
                RootPanel.Children.Add(BuildRuleCard(rule));
        }

        // ── Empty state ──────────────────────────────────────────────────────

        private UIElement BuildEmptyState()
        {
            var panel = new SpacedPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(32, 48, 32, 0),
                Spacing = 0
            };
            panel.Children.Add(new TextBlock
            {
                Text = "\uE943",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 48,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = "No rules yet",
                FontSize = 16,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 4)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Tap + to add your first rule",
                FontSize = 13,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var addBtn = new Button
            {
                Content = "Add Rule",
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Accent,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(1),
                BorderBrush = Accent,
                Padding = new Thickness(20, 8, 20, 8)
            };
            addBtn.Click += OnAddRuleClick;
            panel.Children.Add(addBtn);
            return panel;
        }

        // ── Rule card ────────────────────────────────────────────────────────

        private UIElement BuildRuleCard(RegexRule rule)
        {
            var card = new Border
            {
                Margin = new Thickness(12, 0, 12, 8),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(10),
                Background = CardBg
            };

            var stack = new SpacedPanel { Spacing = 6 };

            // Top row: name + toggle + edit + delete
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameTb = new TextBlock
            {
                Text = rule.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPri,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameTb, 0);
            topRow.Children.Add(nameTb);

            var toggle = new ToggleSwitch
            {
                IsOn = rule.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, -8, 0)
            };
            toggle.Toggled += (s, e) =>
            {
                if (toggle.IsOn != rule.Enabled)
                    _vm.ToggleRule(rule.Id);
            };
            Grid.SetColumn(toggle, 1);
            topRow.Children.Add(toggle);

            var editBtn = MakeIconButton("\uE70F", Accent);
            editBtn.Margin = new Thickness(4, 0, 0, 0);
            editBtn.Click += async (s, e) =>
            {
                var updated = await ShowRuleDialog(rule);
                if (updated == null) return;
                _vm.UpdateRule(rule.Id, updated.Name, updated.Pattern, updated.IsRegex, updated.Replacement,
                    updated.ApplyToOutput, updated.ApplyToInput, updated.CaseInsensitive);
                Rebuild();
            };
            Grid.SetColumn(editBtn, 2);
            topRow.Children.Add(editBtn);

            var delBtn = MakeIconButton("\uE74D", ErrorBrush);
            delBtn.Margin = new Thickness(4, 0, 0, 0);
            delBtn.Click += async (s, e) =>
            {
                var confirm = new ContentDialog
                {
                    Title = $"Delete \"{rule.Name}\"?",
                    Content = "This rule will be permanently deleted.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close
                };
                var res = await confirm.ShowAsync();
                if (res == ContentDialogResult.Primary) { _vm.DeleteRule(rule.Id); Rebuild(); }
            };
            Grid.SetColumn(delBtn, 3);
            topRow.Children.Add(delBtn);

            stack.Children.Add(topRow);

            // Pattern row
            var patternTrunc = rule.Pattern.Length > 40 ? rule.Pattern.Substring(0, 40) + "…" : rule.Pattern;
            var replTrunc = string.IsNullOrEmpty(rule.Replacement)
                ? "(remove)"
                : (rule.Replacement.Length > 20 ? rule.Replacement.Substring(0, 20) + "…" : rule.Replacement);

            var patternRow = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            patternRow.Children.Add(new TextBlock
            {
                Text = rule.IsRegex ? "Regex:" : "Text:",
                FontSize = 11,
                Foreground = Accent,
                VerticalAlignment = VerticalAlignment.Center
            });
            patternRow.Children.Add(new TextBlock
            {
                Text = patternTrunc + " → " + replTrunc,
                FontSize = 12,
                Foreground = TextSec,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(patternRow);

            // Scope badges
            if (rule.ApplyToOutput || rule.ApplyToInput)
            {
                var badgeRow = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                if (rule.ApplyToOutput) badgeRow.Children.Add(MakeBadge("Output"));
                if (rule.ApplyToInput)  badgeRow.Children.Add(MakeBadge("Input"));
                stack.Children.Add(badgeRow);
            }

            card.Child = stack;
            return card;
        }

        // ── Add/Edit Rule dialog ─────────────────────────────────────────────

        private async System.Threading.Tasks.Task<RegexRule> ShowRuleDialog(RegexRule existing)
        {
            var isEdit = existing != null;

            var nameBox = new TextBox
            {
                Header = "Rule name",
                PlaceholderText = "Required",
                Text = isEdit ? existing.Name : ""
            };

            var patternLabel = new TextBlock
            {
                Text = (isEdit && existing.IsRegex) ? "Pattern (regex)" : "Pattern",
                FontSize = 13,
                Margin = new Thickness(0, 4, 0, 2)
            };
            var patternBox = new TextBox
            {
                PlaceholderText = "Required",
                Text = isEdit ? existing.Pattern : ""
            };

            var isRegexToggle = new ToggleSwitch
            {
                Header = "Regex pattern",
                IsOn = isEdit && existing.IsRegex
            };
            isRegexToggle.Toggled += (s, e) =>
                patternLabel.Text = isRegexToggle.IsOn ? "Pattern (regex)" : "Pattern";

            var replaceBox = new TextBox
            {
                Header = "Replace with",
                PlaceholderText = "Leave empty to remove matches",
                Text = isEdit ? existing.Replacement : ""
            };

            var chkOutput = new CheckBox
            {
                Content = "Apply to AI output",
                IsChecked = !isEdit || existing.ApplyToOutput
            };
            var chkInput = new CheckBox
            {
                Content = "Apply to my input",
                IsChecked = isEdit && existing.ApplyToInput
            };
            var chkCase = new CheckBox
            {
                Content = "Case insensitive",
                IsChecked = isEdit && existing.CaseInsensitive
            };

            // Live preview
            var testInputBox = new TextBox
            {
                Header = "Test input",
                PlaceholderText = "Type something to preview the result…"
            };
            var previewLabel = new TextBlock
            {
                Text = "Result: —",
                FontSize = 12,
                Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            };

            Action updatePreview = () =>
            {
                var input = testInputBox.Text;
                if (string.IsNullOrEmpty(input)) { previewLabel.Text = "Result: —"; return; }
                try
                {
                    string result;
                    if (isRegexToggle.IsOn)
                    {
                        var opts = chkCase.IsChecked == true
                            ? RegexOptions.IgnoreCase
                            : RegexOptions.None;
                        result = Regex.Replace(input, patternBox.Text, replaceBox.Text, opts);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(patternBox.Text))
                        {
                            result = input;
                        }
                        else if (chkCase.IsChecked == true)
                        {
                            // Case-insensitive plain-text replace
                            var comparison = StringComparison.OrdinalIgnoreCase;
                            var sb = new System.Text.StringBuilder();
                            int i = 0;
                            while (i < input.Length)
                            {
                                int idx = input.IndexOf(patternBox.Text, i, comparison);
                                if (idx < 0) { sb.Append(input.Substring(i)); break; }
                                sb.Append(input.Substring(i, idx - i));
                                sb.Append(replaceBox.Text);
                                i = idx + patternBox.Text.Length;
                            }
                            result = sb.ToString();
                        }
                        else
                        {
                            result = input.Replace(patternBox.Text, replaceBox.Text);
                        }
                    }
                    previewLabel.Text = "Result: " + result;
                    previewLabel.Foreground = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
                }
                catch (Exception ex)
                {
                    previewLabel.Text = "Error: " + ex.Message;
                    previewLabel.Foreground = ErrorBrush;
                }
            };

            testInputBox.TextChanged += (s, e) => updatePreview();
            patternBox.TextChanged += (s, e) => updatePreview();
            replaceBox.TextChanged += (s, e) => updatePreview();
            isRegexToggle.Toggled += (s, e) => updatePreview();
            chkCase.Checked += (s, e) => updatePreview();
            chkCase.Unchecked += (s, e) => updatePreview();

            var content = new SpacedPanel { Spacing = 10, MinWidth = 300 };
            content.Children.Add(nameBox);
            content.Children.Add(patternLabel);
            content.Children.Add(patternBox);
            content.Children.Add(isRegexToggle);
            content.Children.Add(replaceBox);
            content.Children.Add(chkOutput);
            content.Children.Add(chkInput);
            content.Children.Add(chkCase);
            content.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = (SolidColorBrush)Application.Current.Resources["BackgroundSurfaceBrush"],
                Margin = new Thickness(0, 4, 0, 0)
            });
            content.Children.Add(testInputBox);
            content.Children.Add(previewLabel);

            var dialog = new ContentDialog
            {
                Title = isEdit ? "Edit Rule" : "Add Rule",
                Content = new ScrollViewer { Content = content, MaxHeight = 520 },
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result2 = await dialog.ShowAsync();
            if (result2 != ContentDialogResult.Primary) return null;

            var name = nameBox.Text.Trim();
            var pattern = patternBox.Text.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pattern)) return null;

            return new RegexRule
            {
                Id = isEdit ? existing.Id : Guid.NewGuid().ToString(),
                Name = name,
                Pattern = pattern,
                IsRegex = isRegexToggle.IsOn,
                Replacement = replaceBox.Text,
                ApplyToOutput = chkOutput.IsChecked == true,
                ApplyToInput = chkInput.IsChecked == true,
                CaseInsensitive = chkCase.IsChecked == true,
                Enabled = isEdit ? existing.Enabled : true
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Button MakeIconButton(string glyph, SolidColorBrush brush)
        {
            return new Button
            {
                Content = new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = brush
                },
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private UIElement MakeBadge(string text)
        {
            return new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Accent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 10,
                    Foreground = Accent
                }
            };
        }
    }
}
