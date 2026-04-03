using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.ViewModels
{
    public class ImageGenSettingsViewModel : ViewModelBase
    {
        private ImageGenConfig _config;
        public ImageGenConfig Config
        {
            get => _config;
            set => Set(ref _config, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private string _testResult;
        public string TestResult
        {
            get => _testResult;
            set => Set(ref _testResult, value);
        }

        public void Load()
        {
            Config = App.Settings.GetImageGenConfig();
        }

        public void Save()
        {
            if (_config != null)
                App.Settings.SaveImageGenConfig(_config);
        }

        public void SetBackend(string backend)
        {
            if (_config == null) return;
            _config.ActiveBackend = backend;
            Save();
        }

        public async Task<List<string>> FetchSamplersAsync()
        {
            var url = _config?.SdWebuiUrl;
            if (string.IsNullOrWhiteSpace(url)) return new List<string>();
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    var resp = await client.GetAsync(url.TrimEnd('/') + "/sdapi/v1/samplers");
                    if (!resp.IsSuccessStatusCode) return new List<string>();
                    var arr = JArray.Parse(await resp.Content.ReadAsStringAsync());
                    return arr.Select(t => t["name"]?.ToString()).Where(n => n != null).ToList();
                }
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> FetchModelsAsync()
        {
            var url = _config?.SdWebuiUrl;
            if (string.IsNullOrWhiteSpace(url)) return new List<string>();
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    var resp = await client.GetAsync(url.TrimEnd('/') + "/sdapi/v1/sd-models");
                    if (!resp.IsSuccessStatusCode) return new List<string>();
                    var arr = JArray.Parse(await resp.Content.ReadAsStringAsync());
                    return arr.Select(t => t["title"]?.ToString()).Where(n => n != null).ToList();
                }
            }
            catch { return new List<string>(); }
        }

        public async Task TestConnectionAsync()
        {
            if (_config == null) return;

            string url = null;
            if (_config.ActiveBackend == "SD_WEBUI")
                url = _config.SdWebuiUrl?.TrimEnd('/') + "/sdapi/v1/options";
            else if (_config.ActiveBackend == "COMFYUI")
                url = _config.ComfyuiUrl?.TrimEnd('/') + "/system_stats";

            if (string.IsNullOrWhiteSpace(url))
            {
                TestResult = "No URL configured.";
                return;
            }

            IsLoading = true;
            TestResult = "Testing...";
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(8);
                    var resp = await client.GetAsync(url);
                    TestResult = resp.IsSuccessStatusCode
                        ? $"Connected ({(int)resp.StatusCode})"
                        : $"Error: {(int)resp.StatusCode} {resp.ReasonPhrase}";
                }
            }
            catch (Exception ex)
            {
                TestResult = $"Failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
