using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;
using PocketTavern.UWP.ViewModels;

namespace PocketTavern.UWP.Views
{
    public sealed partial class TextGenPage : Page
    {
        private readonly TextGenViewModel _vm = new TextGenViewModel();
        public TextGenPage() { this.InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _vm.LoadAsync();
            PresetCombo.ItemsSource = _vm.PresetNames;
            if (!string.IsNullOrEmpty(_vm.SelectedPreset))
                PresetCombo.SelectedItem = _vm.SelectedPreset;
            LoadCurrentValues();
        }

        private void LoadCurrentValues()
        {
            var p = _vm.Current;

            TempSlider.Value         = p.Temperature;
            TopPSlider.Value         = p.TopP;
            MinPSlider.Value         = p.MinP;
            TopASlider.Value         = p.TopA;
            TypicalPSlider.Value     = p.TypicalP;
            TfsSlider.Value          = p.Tfs;
            TopKBox.Text             = p.TopK.ToString();
            MaxTokensBox.Text        = p.MaxNewTokens?.ToString() ?? "";
            MinTokensBox.Text        = p.MinTokens.ToString();
            ContextLenBox.Text       = p.TruncationLength.ToString();

            RepPenSlider.Value       = p.RepPen;
            RepPenRangeBox.Text      = p.RepPenRange.ToString();
            RepPenSlopeSlider.Value  = p.RepPenSlope;
            FreqPenSlider.Value      = p.FrequencyPenalty;
            PresPenSlider.Value      = p.PresencePenalty;

            DryMultSlider.Value      = p.DryMultiplier;
            DryBaseSlider.Value      = p.DryBase;
            DryAllowedLenBox.Text    = p.DryAllowedLength.ToString();
            DryLastNBox.Text         = p.DryPenaltyLastN.ToString();

            MirostatModeCombo.SelectedIndex = p.MirostatMode;
            MiroTauSlider.Value      = p.MirostatTau;
            MiroEtaSlider.Value      = p.MirostatEta;

            XtcThreshSlider.Value    = p.XtcThreshold;
            XtcProbSlider.Value      = p.XtcProbability;

            SkewSlider.Value            = p.Skew;
            SmoothingFactorSlider.Value = p.SmoothingFactor;
            SmoothingCurveSlider.Value  = p.SmoothingCurve;
            GuidanceScaleSlider.Value   = p.GuidanceScale;

            AddBosCheck.IsChecked    = p.AddBosToken;
            BanEosCheck.IsChecked    = p.BanEosToken;
            SkipSpecialCheck.IsChecked = p.SkipSpecialTokens;

            // Update all labels
            UpdateLabel(TempLabel,        p.Temperature,       "0.00");
            UpdateLabel(TopPLabel,        p.TopP,              "0.00");
            UpdateLabel(MinPLabel,        p.MinP,              "0.00");
            UpdateLabel(TopALabel,        p.TopA,              "0.00");
            UpdateLabel(TypicalPLabel,    p.TypicalP,          "0.00");
            UpdateLabel(TfsLabel,         p.Tfs,               "0.00");
            UpdateLabel(RepPenLabel,      p.RepPen,            "0.00");
            UpdateLabel(RepPenSlopeLabel, p.RepPenSlope,       "0.0");
            UpdateLabel(FreqPenLabel,     p.FrequencyPenalty,  "0.00");
            UpdateLabel(PresPenLabel,     p.PresencePenalty,   "0.00");
            UpdateLabel(DryMultLabel,     p.DryMultiplier,     "0.00");
            UpdateLabel(DryBaseLabel,     p.DryBase,           "0.00");
            UpdateLabel(MiroTauLabel,     p.MirostatTau,       "0.0");
            UpdateLabel(MiroEtaLabel,     p.MirostatEta,       "0.00");
            UpdateLabel(XtcThreshLabel,       p.XtcThreshold,      "0.00");
            UpdateLabel(XtcProbLabel,         p.XtcProbability,    "0.00");
            UpdateLabel(SkewLabel,            p.Skew,              "0.00");
            UpdateLabel(SmoothingFactorLabel, p.SmoothingFactor,   "0.00");
            UpdateLabel(SmoothingCurveLabel,  p.SmoothingCurve,    "0.00");
            UpdateLabel(GuidanceScaleLabel,   p.GuidanceScale,     "0.00");
        }

        private static void UpdateLabel(TextBlock lbl, double value, string fmt)
            => lbl.Text = value.ToString(fmt);

        private void OnSliderLabelChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!((sender as Slider)?.Tag is string tagName)) return;
            var lbl = FindName(tagName) as TextBlock;
            if (lbl != null) lbl.Text = e.NewValue.ToString("0.00");
        }

        private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetCombo.SelectedItem is string name)
            {
                _vm.SelectedPreset = name;
                LoadCurrentValues();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => App.Navigation.GoBack();

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var p = _vm.Current;

            p.Temperature       = (float)TempSlider.Value;
            p.TopP              = (float)TopPSlider.Value;
            p.MinP              = (float)MinPSlider.Value;
            p.TopA              = (float)TopASlider.Value;
            p.TypicalP          = (float)TypicalPSlider.Value;
            p.Tfs               = (float)TfsSlider.Value;
            if (int.TryParse(TopKBox.Text,      out int topK))      p.TopK = topK;
            if (int.TryParse(MaxTokensBox.Text,  out int mt))       p.MaxNewTokens = mt;
            else                                                      p.MaxNewTokens = null;
            if (int.TryParse(MinTokensBox.Text,  out int minT))     p.MinTokens = minT;
            if (int.TryParse(ContextLenBox.Text, out int ctx))      p.TruncationLength = ctx;

            p.RepPen            = (float)RepPenSlider.Value;
            if (int.TryParse(RepPenRangeBox.Text, out int rpr))     p.RepPenRange = rpr;
            p.RepPenSlope       = (float)RepPenSlopeSlider.Value;
            p.FrequencyPenalty  = (float)FreqPenSlider.Value;
            p.PresencePenalty   = (float)PresPenSlider.Value;

            p.DryMultiplier     = (float)DryMultSlider.Value;
            p.DryBase           = (float)DryBaseSlider.Value;
            if (int.TryParse(DryAllowedLenBox.Text, out int dal))   p.DryAllowedLength = dal;
            if (int.TryParse(DryLastNBox.Text,       out int dln))  p.DryPenaltyLastN = dln;

            p.MirostatMode      = MirostatModeCombo.SelectedIndex;
            p.MirostatTau       = (float)MiroTauSlider.Value;
            p.MirostatEta       = (float)MiroEtaSlider.Value;

            p.XtcThreshold      = (float)XtcThreshSlider.Value;
            p.XtcProbability    = (float)XtcProbSlider.Value;

            p.Skew              = (float)SkewSlider.Value;
            p.SmoothingFactor   = (float)SmoothingFactorSlider.Value;
            p.SmoothingCurve    = (float)SmoothingCurveSlider.Value;
            p.GuidanceScale     = (float)GuidanceScaleSlider.Value;

            p.AddBosToken       = AddBosCheck.IsChecked ?? true;
            p.BanEosToken       = BanEosCheck.IsChecked ?? false;
            p.SkipSpecialTokens = SkipSpecialCheck.IsChecked ?? true;

            await _vm.SaveAsync();
        }
    }
}
