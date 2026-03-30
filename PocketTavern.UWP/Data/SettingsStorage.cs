using Windows.Storage;
using PocketTavern.UWP.Models;
using Newtonsoft.Json;

namespace PocketTavern.UWP.Data
{
    /// <summary>
    /// Persists app settings to ApplicationData.LocalSettings — equivalent of Android DataStore.
    /// </summary>
    public class SettingsStorage
    {
        private readonly ApplicationDataContainer _settings =
            ApplicationData.Current.LocalSettings;

        // ── LLM Config ──────────────────────────────────────────────────────────

        public ApiConfiguration GetLlmConfig()
        {
            return new ApiConfiguration
            {
                MainApi = Get("llm_main_api", "textgenerationwebui"),
                TextGenType = Get("llm_text_gen_type", "koboldcpp"),
                ApiServer = Get("llm_api_server", "http://127.0.0.1:5001"),
                ChatCompletionSource = Get("llm_chat_completion_source", "openai"),
                CustomUrl = GetOrNull("llm_custom_url"),
                ApiKey = Get("llm_api_key", ""),
                CurrentModel = Get("llm_current_model", "")
            };
        }

        public void SaveLlmConfig(ApiConfiguration config)
        {
            Set("llm_main_api", config.MainApi);
            Set("llm_text_gen_type", config.TextGenType);
            Set("llm_api_server", config.ApiServer?.TrimEnd('/') ?? "");
            Set("llm_chat_completion_source", config.ChatCompletionSource);
            if (config.CustomUrl != null) Set("llm_custom_url", config.CustomUrl);
            else Remove("llm_custom_url");
            Set("llm_api_key", config.ApiKey ?? "");
            Set("llm_current_model", config.CurrentModel ?? "");
        }

        // ── Preset Selections ────────────────────────────────────────────────────

        public string GetSelectedTextGenPreset() => GetOrNull("selected_textgen_preset");
        public void SetSelectedTextGenPreset(string name) { if (name != null) Set("selected_textgen_preset", name); else Remove("selected_textgen_preset"); }

        public string GetSelectedOaiPreset() => GetOrNull("selected_oai_preset");
        public void SetSelectedOaiPreset(string name) { if (name != null) Set("selected_oai_preset", name); else Remove("selected_oai_preset"); }

        public string GetSelectedInstructPreset() => GetOrNull("selected_instruct_preset");
        public void SetSelectedInstructPreset(string name) { if (name != null) Set("selected_instruct_preset", name); else Remove("selected_instruct_preset"); }

        public string GetSelectedSyspromptPreset() => GetOrNull("selected_sysprompt_preset");
        public void SetSelectedSyspromptPreset(string name) { if (name != null) Set("selected_sysprompt_preset", name); else Remove("selected_sysprompt_preset"); }

        public string GetSelectedContextPreset() => GetOrNull("selected_context_preset");
        public void SetSelectedContextPreset(string name) { if (name != null) Set("selected_context_preset", name); else Remove("selected_context_preset"); }

        // ── CharaVault ───────────────────────────────────────────────────────────

        public string GetCharaVaultUrl() => Get("cardvault_url", "");
        public void SaveCharaVaultUrl(string url) => Set("cardvault_url", url?.TrimEnd('/') ?? "");

        public string GetCharaVaultToken() => GetOrNull("charavault_token");
        public string GetCharaVaultEmail() => GetOrNull("charavault_email");
        public void SaveCharaVaultSession(string token, string email) { Set("charavault_token", token); Set("charavault_email", email); }
        public void ClearCharaVaultSession() { Remove("charavault_token"); Remove("charavault_email"); }

        public string GetCharaVaultMode() => Get("charavault_mode", "local");
        public void SaveCharaVaultMode(string mode) => Set("charavault_mode", mode);

        // ── External URLs ────────────────────────────────────────────────────────

        public string GetForgeUrl() => Get("sillytavern_forge", "");
        public void SaveForgeUrl(string url) => Set("sillytavern_forge", url?.TrimEnd('/') ?? "");

        public string GetProxyUrl() => Get("sillytavern_proxy", "");
        public void SaveProxyUrl(string url) => Set("sillytavern_proxy", url?.TrimEnd('/') ?? "");

        // ── User Persona ─────────────────────────────────────────────────────────

        public string GetUserPersonaName() => Get("user_persona_name", "User");
        public void SaveUserPersonaName(string name) => Set("user_persona_name", name);

        public string GetUserPersonaDesc() => Get("user_persona_desc", "");
        public void SaveUserPersonaDesc(string desc) => Set("user_persona_desc", desc);

        public int GetUserPersonaPosition() => GetInt("user_persona_position", 0);
        public void SaveUserPersonaPosition(int pos) => SetInt("user_persona_position", pos);

        public int GetUserPersonaDepth() => GetInt("user_persona_depth", 2);
        public void SaveUserPersonaDepth(int depth) => SetInt("user_persona_depth", depth);

        public int GetUserPersonaRole() => GetInt("user_persona_role", 0);
        public void SaveUserPersonaRole(int role) => SetInt("user_persona_role", role);

        public string GetCustomSystemPrompt() => Get("custom_system_prompt", "");
        public void SaveCustomSystemPrompt(string prompt) => Set("custom_system_prompt", prompt);

        // ── Auto-Continue ─────────────────────────────────────────────────────────

        public bool GetAutoContinueEnabled() => GetBool("auto_continue_enabled", false);
        public int GetAutoContinueMinLength() => GetInt("auto_continue_min_length", 200);
        public void SaveAutoContinueConfig(bool enabled, int minLength) { SetBool("auto_continue_enabled", enabled); SetInt("auto_continue_min_length", minLength); }

        // ── Connection Profile ────────────────────────────────────────────────────

        public string GetLastActivatedProfileId() => GetOrNull("last_activated_profile_id");
        public void SetLastActivatedProfileId(string id) { if (id != null) Set("last_activated_profile_id", id); else Remove("last_activated_profile_id"); }

        // ── Global Author's Note ──────────────────────────────────────────────────

        public string GetGlobalAuthorsNoteContent() => Get("global_authors_note_content", "");
        public int GetGlobalAuthorsNoteDepth() => GetInt("global_authors_note_depth", 4);
        public int GetGlobalAuthorsNoteInterval() => GetInt("global_authors_note_interval", 1);
        public int GetGlobalAuthorsNotePosition() => GetInt("global_authors_note_position", 0);
        public int GetGlobalAuthorsNoteRole() => GetInt("global_authors_note_role", 0);

        public void SaveGlobalAuthorsNote(string content, int depth, int interval, int position, int role)
        {
            Set("global_authors_note_content", content);
            SetInt("global_authors_note_depth", depth);
            SetInt("global_authors_note_interval", interval);
            SetInt("global_authors_note_position", position);
            SetInt("global_authors_note_role", role);
        }

        // ── TTS ──────────────────────────────────────────────────────────────────

        public TtsConfig GetTtsConfig() => new TtsConfig
        {
            Enabled = GetBool("tts_enabled", false),
            Provider = Get("tts_provider", "system"),
            AutoPlay = GetBool("tts_auto_play", true),
            OpenAiUrl = Get("tts_openai_url", ""),
            OpenAiKey = Get("tts_openai_key", ""),
            OpenAiVoice = Get("tts_openai_voice", "alloy"),
            OpenAiModel = Get("tts_openai_model", "tts-1"),
            Speed = GetFloat("tts_speed", 1.0f),
            FilterMode = Get("tts_filter_mode", "all")
        };

        public void SaveTtsConfig(TtsConfig c)
        {
            SetBool("tts_enabled", c.Enabled);
            Set("tts_provider", c.Provider);
            SetBool("tts_auto_play", c.AutoPlay);
            Set("tts_openai_url", c.OpenAiUrl);
            Set("tts_openai_key", c.OpenAiKey);
            Set("tts_openai_voice", c.OpenAiVoice);
            Set("tts_openai_model", c.OpenAiModel);
            SetFloat("tts_speed", c.Speed);
            Set("tts_filter_mode", c.FilterMode);
        }

        // ── Image Generation ─────────────────────────────────────────────────────

        public ImageGenConfig GetImageGenConfig()
        {
            var raw = GetOrNull("image_gen_config");
            if (raw != null)
            {
                try { return JsonConvert.DeserializeObject<ImageGenConfig>(raw) ?? new ImageGenConfig(); }
                catch { }
            }
            return new ImageGenConfig { SdWebuiUrl = GetForgeUrl() };
        }

        public void SaveImageGenConfig(ImageGenConfig c)
        {
            Set("image_gen_config", JsonConvert.SerializeObject(c));
            Set("sillytavern_forge", c.SdWebuiUrl?.TrimEnd('/') ?? "");
        }

        // ── Native Extensions ─────────────────────────────────────────────────────

        public bool GetExtQuickReplyEnabled() => GetBool("ext_quick_reply_enabled", true);
        public void SetExtQuickReplyEnabled(bool v) => SetBool("ext_quick_reply_enabled", v);

        public bool GetExtRegexEnabled() => GetBool("ext_regex_enabled", true);
        public void SetExtRegexEnabled(bool v) => SetBool("ext_regex_enabled", v);

        public bool GetExtTokenCounterEnabled() => GetBool("ext_token_counter_enabled", false);
        public void SetExtTokenCounterEnabled(bool v) => SetBool("ext_token_counter_enabled", v);

        // ── Theme ─────────────────────────────────────────────────────────────────

        public string GetThemeKey() => Get("app_theme_key", "default");
        public void SaveThemeKey(string key) => Set("app_theme_key", key);

        public void ClearAll() => _settings.Values.Clear();

        // ── Helpers ──────────────────────────────────────────────────────────────

        private string Get(string key, string defaultValue) =>
            _settings.Values.ContainsKey(key) ? (string)_settings.Values[key] : defaultValue;

        private string GetOrNull(string key) =>
            _settings.Values.ContainsKey(key) ? (string)_settings.Values[key] : null;

        private void Set(string key, string value) => _settings.Values[key] = value;
        private void Remove(string key) { if (_settings.Values.ContainsKey(key)) _settings.Values.Remove(key); }

        private bool GetBool(string key, bool def) =>
            _settings.Values.ContainsKey(key) ? (bool)_settings.Values[key] : def;
        private void SetBool(string key, bool value) => _settings.Values[key] = value;

        private int GetInt(string key, int def) =>
            _settings.Values.ContainsKey(key) ? (int)_settings.Values[key] : def;
        private void SetInt(string key, int value) => _settings.Values[key] = value;

        private float GetFloat(string key, float def) =>
            _settings.Values.ContainsKey(key) ? (float)_settings.Values[key] : def;
        private void SetFloat(string key, float value) => _settings.Values[key] = value;
    }
}
