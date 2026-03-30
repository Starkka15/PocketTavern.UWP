using System.Collections.ObjectModel;
using System.IO;
using Windows.Storage;

namespace PocketTavern.UWP.ViewModels
{
    public class WorldInfoItem
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string EntryCount { get; set; }
    }

    public class WorldInfoViewModel : ViewModelBase
    {
        private ObservableCollection<WorldInfoItem> _items = new ObservableCollection<WorldInfoItem>();
        public ObservableCollection<WorldInfoItem> Items { get => _items; set => Set(ref _items, value); }

        public void Load()
        {
            var dir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "worlds");
            Directory.CreateDirectory(dir);
            Items.Clear();
            foreach (var f in Directory.GetFiles(dir, "*.json"))
            {
                int entryCount = 0;
                try
                {
                    var json = File.ReadAllText(f);
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                    entryCount = (obj["entries"] as Newtonsoft.Json.Linq.JArray)?.Count
                              ?? (obj["entries"] as Newtonsoft.Json.Linq.JObject)?.Count
                              ?? 0;
                }
                catch { }
                Items.Add(new WorldInfoItem
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    FilePath = f,
                    EntryCount = entryCount > 0 ? $"{entryCount} entries" : "No entries"
                });
            }
        }
    }
}
