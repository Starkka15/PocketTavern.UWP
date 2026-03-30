using System.Collections.Generic;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class OaiPromptOrderItemViewModel : ViewModelBase
    {
        private bool _isExpanded;

        public OaiPromptOrderItem Item { get; }

        public OaiPromptOrderItemViewModel(OaiPromptOrderItem item) { Item = item; }

        public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

        public bool Enabled
        {
            get => Item.Enabled;
            set { Item.Enabled = value; OnPropertyChanged(); }
        }

        public string Content
        {
            get => Item.Content ?? "";
            set { Item.Content = string.IsNullOrEmpty(value) ? null : value; OnPropertyChanged(); }
        }

        public int RoleIndex
        {
            get
            {
                switch (Item.Role)
                {
                    case "user":      return 1;
                    case "assistant": return 2;
                    default:          return 0;
                }
            }
            set
            {
                switch (value)
                {
                    case 1:  Item.Role = "user";      break;
                    case 2:  Item.Role = "assistant"; break;
                    default: Item.Role = "system";    break;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtitle));
            }
        }

        public int InjectionPosition
        {
            get => Item.InjectionPosition;
            set
            {
                Item.InjectionPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDepthInjected));
                OnPropertyChanged(nameof(Subtitle));
            }
        }

        public string InjectionDepthText
        {
            get => Item.InjectionDepth.ToString();
            set
            {
                if (int.TryParse(value, out int d))
                {
                    Item.InjectionDepth = System.Math.Max(0, d);
                    OnPropertyChanged(nameof(Subtitle));
                }
                OnPropertyChanged();
            }
        }

        public bool IsMarker  => Item.IsMarker;
        public bool IsCustom  => Item.IsCustom;
        public bool IsEditable => !Item.IsMarker;
        public string Label   => Item.Label;
        public bool IsDepthInjected => InjectionPosition == 1;

        public string Subtitle
        {
            get
            {
                if (IsMarker) return "Dynamic content";
                var parts = new List<string> { Item.Role };
                if (InjectionPosition == 1) parts.Add($"depth {Item.InjectionDepth}");
                if (IsCustom) parts.Add("custom");
                return string.Join(" · ", parts);
            }
        }
    }
}
