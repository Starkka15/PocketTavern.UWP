using System.Collections.ObjectModel;

namespace PocketTavern.UWP.ViewModels
{
    public class ExtensionItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; }
    }

    public class ExtensionsViewModel : ViewModelBase
    {
        private ObservableCollection<ExtensionItem> _extensions = new ObservableCollection<ExtensionItem>();
        public ObservableCollection<ExtensionItem> Extensions { get => _extensions; set => Set(ref _extensions, value); }

        public void Load()
        {
            // Built-in extensions
            Extensions.Clear();
            Extensions.Add(new ExtensionItem { Id = "quick_reply", Name = "Quick Reply", Description = "Add quick reply buttons to the chat", Enabled = true });
            Extensions.Add(new ExtensionItem { Id = "regex", Name = "Regex", Description = "Apply regex substitutions to messages", Enabled = true });
            Extensions.Add(new ExtensionItem { Id = "token_counter", Name = "Token Counter", Description = "Show estimated token count", Enabled = false });
        }
    }
}
