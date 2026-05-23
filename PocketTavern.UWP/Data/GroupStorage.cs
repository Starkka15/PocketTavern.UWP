using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Data
{
    public class GroupStorage
    {
        private readonly string _dir;

        public GroupStorage()
        {
            _dir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "groups");
            Directory.CreateDirectory(_dir);
        }

        public async Task<List<Group>> GetAllGroupsAsync()
        {
            var result = new List<Group>();
            foreach (var file in Directory.GetFiles(_dir, "*.json"))
            {
                try
                {
                    var g = JsonConvert.DeserializeObject<Group>(File.ReadAllText(file));
                    if (g != null) result.Add(g);
                }
                catch { }
            }
            return result;
        }

        public Group GetGroup(string id)
        {
            var path = Path.Combine(_dir, id + ".json");
            if (!File.Exists(path)) return null;
            try { return JsonConvert.DeserializeObject<Group>(File.ReadAllText(path)); }
            catch { return null; }
        }

        public Group CreateGroup(string name, List<string> members)
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 16);
            var group = new Group { Id = id, Name = name, Members = members ?? new List<string>() };
            File.WriteAllText(Path.Combine(_dir, id + ".json"),
                JsonConvert.SerializeObject(group, Formatting.Indented));
            return group;
        }

        public async Task SaveGroupAsync(Group group)
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(_dir);
            var file = await folder.CreateFileAsync(group.Id + ".json",
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file,
                JsonConvert.SerializeObject(group, Formatting.Indented));
        }

        public void DeleteGroup(string id)
        {
            var path = Path.Combine(_dir, id + ".json");
            if (File.Exists(path)) File.Delete(path);
        }

        // Reserved character-folder prefix for group chats in ChatStorage
        public string ChatFolderName(string groupId) => $"__group__{groupId}";
    }
}
