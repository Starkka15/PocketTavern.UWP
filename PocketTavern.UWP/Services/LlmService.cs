using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// Handles LLM generation requests for OpenAI-compatible (chat completion)
    /// and text-generation (KoboldCpp / llama.cpp / Ooba) backends.
    /// Streams tokens via IProgress&lt;StreamEvent&gt;.
    /// </summary>
    public class LlmService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(300)
        };

        // ── Chat Completion (OpenAI-compatible) ──────────────────────────────────

        /// <summary>
        /// Called with a pre-built ordered message list (mirrors Android PromptBuilder output).
        /// </summary>
        public async Task GenerateChatCompletionAsync(
            ApiConfiguration config,
            OaiPreset preset,
            List<JObject> prebuiltMessages,
            IProgress<StreamEvent> progress,
            CancellationToken ct = default)
        {
            // Pre-flight: cloud providers require a model and an API key
            if (string.IsNullOrWhiteSpace(config.CurrentModel))
            {
                progress?.Report(new StreamEvent.Error { Message = "No model selected — open API Config, fetch the model list, select a model, and tap Save." });
                return;
            }
            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                progress?.Report(new StreamEvent.Error { Message = "API key is empty — open API Config, enter your API key, and tap Save." });
                return;
            }

            var body = BuildChatCompletionBody(preset, prebuiltMessages, config.CurrentModel);
            var url = config.ChatCompletionBaseUrl.TrimEnd('/') + "/v1/chat/completions";
            await StreamRequestAsync(url, config.ApiKey.Trim(), body, progress, ct, isClaude: config.ChatCompletionSource == "claude");
        }

        /// <summary>Legacy overload — builds messages from a flat history + system prompt string.</summary>
        public async Task GenerateChatCompletionAsync(
            ApiConfiguration config,
            OaiPreset preset,
            List<ChatMessage> history,
            string systemPrompt,
            IProgress<StreamEvent> progress,
            CancellationToken ct = default)
        {
            var messages = BuildChatMessages(history, systemPrompt);
            var body = BuildChatCompletionBody(preset, messages, config.CurrentModel);
            var url = config.ChatCompletionBaseUrl.TrimEnd('/') + "/v1/chat/completions";
            await StreamRequestAsync(url, config.ApiKey, body, progress, ct, isClaude: config.ChatCompletionSource == "claude");
        }

        // ── Text Generation (KoboldCpp / llama.cpp / Ooba) ──────────────────────

        public async Task GenerateTextGenAsync(
            ApiConfiguration config,
            TextGenPreset preset,
            string prompt,
            IProgress<StreamEvent> progress,
            CancellationToken ct = default)
        {
            string url;
            JObject body;

            switch (config.TextGenType?.ToLowerInvariant())
            {
                case "koboldcpp":
                    url = config.ApiServer.TrimEnd('/') + "/api/extra/generate/stream";
                    body = BuildKoboldBody(preset, prompt);
                    break;
                case "llamacpp":
                    url = config.ApiServer.TrimEnd('/') + "/completion";
                    body = BuildLlamaCppBody(preset, prompt);
                    break;
                default: // ooba, vllm, etc. — use OpenAI-compatible text endpoint
                    url = config.ApiServer.TrimEnd('/') + "/v1/completions";
                    body = BuildTextCompletionBody(preset, prompt);
                    break;
            }

            await StreamRequestAsync(url, config.ApiKey, body, progress, ct);
        }

        // ── Model listing ────────────────────────────────────────────────────────

        public async Task<List<AvailableModel>> GetAvailableModelsAsync(ApiConfiguration config)
        {
            try
            {
                var type = config.TextGenType?.ToLowerInvariant() ?? "";
                if (config.UsesChatCompletions || IsOaiCompatibleTextGen(type))
                    return await FetchOaiModelsAsync(config);
                if (type == "ollama")
                    return await FetchOllamaModelsAsync(config);
                if (type == "koboldcpp")
                    return await FetchKoboldModelAsync(config);
                return new List<AvailableModel>();
            }
            catch
            {
                return new List<AvailableModel>();
            }
        }

        private static bool IsOaiCompatibleTextGen(string type) =>
            type == "ooba" || type == "vllm" || type == "aphrodite" || type == "tabby" ||
            type == "togetherai" || type == "infermaticai" || type == "openrouter" ||
            type == "featherless" || type == "mancer" || type == "dreamgen" ||
            type == "huggingface" || type == "generic";

        private async Task<List<AvailableModel>> FetchOaiModelsAsync(ApiConfiguration config)
        {
            var baseUrl = config.UsesChatCompletions
                ? config.ChatCompletionBaseUrl.TrimEnd('/')
                : config.ApiServer.TrimEnd('/');

            // Strip trailing /v1 so we don't produce .../v1/v1/models
            if (baseUrl.EndsWith("/v1", System.StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 3);

            var url = baseUrl + "/v1/models";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");

            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                if (config.ChatCompletionSource == "claude")
                {
                    req.Headers.TryAddWithoutValidation("x-api-key", config.ApiKey);
                    req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                }
                else
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            }

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new List<AvailableModel>();

            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var data = json["data"] as JArray;
            if (data == null) return new List<AvailableModel>();

            return data
                .Select(n => new AvailableModel
                {
                    Id = n["id"]?.ToString() ?? "",
                    Name = n["id"]?.ToString() ?? "",
                    ContextLength = n["context_length"]?.Value<int>()
                })
                .Where(m => !string.IsNullOrEmpty(m.Id))
                .OrderBy(m => m.Id)
                .ToList();
        }

        private async Task<List<AvailableModel>> FetchOllamaModelsAsync(ApiConfiguration config)
        {
            var resp = await _http.GetAsync(config.ApiServer.TrimEnd('/') + "/api/tags");
            if (!resp.IsSuccessStatusCode) return new List<AvailableModel>();

            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var models = json["models"] as JArray;
            if (models == null) return new List<AvailableModel>();

            return models
                .Select(n => new AvailableModel { Id = n["name"]?.ToString() ?? "", Name = n["name"]?.ToString() ?? "" })
                .Where(m => !string.IsNullOrEmpty(m.Id))
                .OrderBy(m => m.Id)
                .ToList();
        }

        private async Task<List<AvailableModel>> FetchKoboldModelAsync(ApiConfiguration config)
        {
            var resp = await _http.GetAsync(config.ApiServer.TrimEnd('/') + "/api/v1/model");
            if (!resp.IsSuccessStatusCode) return new List<AvailableModel>();

            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var result = json["result"]?.ToString();
            if (string.IsNullOrEmpty(result)) return new List<AvailableModel>();
            return new List<AvailableModel> { new AvailableModel { Id = result, Name = result } };
        }

        // ── Connection test ──────────────────────────────────────────────────────

        public async Task<ConnectionTestResult> TestConnectionAsync(ApiConfiguration config)
        {
            try
            {
                string url;
                if (config.UsesChatCompletions)
                    url = config.ChatCompletionBaseUrl.TrimEnd('/') + "/v1/models";
                else
                    url = config.ApiServer.TrimEnd('/') + "/api/v1/model";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(config.ApiKey))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);

                // Try to extract model name
                var model = (string)(obj["data"]?[0]?["id"] ?? obj["result"] ?? obj["model"] ?? "");
                return new ConnectionTestResult { Connected = true, Model = model, Error = null };
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult { Connected = false, Model = "", Error = ex.Message };
            }
        }

        // ── Streaming ────────────────────────────────────────────────────────────

        private async Task StreamRequestAsync(
            string url,
            string apiKey,
            JObject body,
            IProgress<StreamEvent> progress,
            CancellationToken ct,
            bool isClaude = false)
        {
            body["stream"] = true;
            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (!string.IsNullOrEmpty(apiKey))
            {
                if (isClaude)
                {
                    req.Headers.Add("x-api-key", apiKey);
                    req.Headers.Add("anthropic-version", "2023-06-01");
                }
                else
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            try
            {
                var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    string errBody = "";
                    try { errBody = await resp.Content.ReadAsStringAsync(); } catch { }
                    // Try to extract a human-readable message from JSON error bodies
                    if (!string.IsNullOrWhiteSpace(errBody))
                    {
                        try
                        {
                            var errJson = JObject.Parse(errBody);
                            var msg = errJson["error"]?["message"]?.ToString()
                                   ?? errJson["message"]?.ToString()
                                   ?? errJson["detail"]?.ToString();
                            if (!string.IsNullOrEmpty(msg))
                                errBody = msg;
                        }
                        catch { }
                    }
                    var statusMsg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                    if (!string.IsNullOrWhiteSpace(errBody))
                        statusMsg += ": " + errBody;
                    try { progress?.Report(new StreamEvent.Error { Message = statusMsg }); } catch { }
                    return;
                }

                using (var stream = await resp.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    var accumulated = new StringBuilder();
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null && !ct.IsCancellationRequested)
                    {
                        if (line.StartsWith("data: "))
                            line = line.Substring(6);
                        if (line == "[DONE]" || string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            var chunk = JObject.Parse(line);
                            var token = ExtractToken(chunk);
                            if (token != null)
                            {
                                accumulated.Append(token);
                                progress?.Report(new StreamEvent.Token
                                {
                                    TokenText = token,
                                    Accumulated = accumulated.ToString()
                                });
                            }
                        }
                        catch { }
                    }
                    if (!ct.IsCancellationRequested)
                        progress?.Report(new StreamEvent.Complete { FullText = accumulated.ToString() });
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled by user — not an error
            }
            catch (Exception ex)
            {
                try { progress?.Report(new StreamEvent.Error { Message = ex.Message }); } catch { }
            }
        }

        private static string ExtractToken(JObject chunk)
        {
            // OpenAI chat completion chunk
            var delta = chunk["choices"]?[0]?["delta"]?["content"];
            if (delta != null) return (string)delta;

            // OpenAI text completion chunk
            var text = chunk["choices"]?[0]?["text"];
            if (text != null) return (string)text;

            // KoboldCpp
            var token = chunk["token"];
            if (token != null) return (string)token;

            // llama.cpp
            var llama = chunk["content"];
            if (llama != null) return (string)llama;

            // Anthropic SSE
            var anthropic = chunk["delta"]?["text"];
            if (anthropic != null) return (string)anthropic;

            return null;
        }

        // ── Request builders ─────────────────────────────────────────────────────

        private static List<JObject> BuildChatMessages(List<ChatMessage> history, string systemPrompt)
        {
            var messages = new List<JObject>();
            if (!string.IsNullOrEmpty(systemPrompt))
                messages.Add(new JObject { ["role"] = "system", ["content"] = systemPrompt });

            foreach (var m in history)
            {
                if (m.IsNarrator) continue;
                messages.Add(new JObject
                {
                    ["role"] = m.IsUser ? "user" : "assistant",
                    ["content"] = m.Content
                });
            }
            return messages;
        }

        private static JObject BuildChatCompletionBody(OaiPreset preset, List<JObject> messages, string model = null)
        {
            var body = new JObject { ["messages"] = JArray.FromObject(messages) };
            if (!string.IsNullOrWhiteSpace(model)) body["model"] = model;
            if (preset.TemperatureEnabled) body["temperature"] = preset.Temperature;
            if (preset.MaxTokensEnabled)   body["max_tokens"]   = preset.MaxTokens;
            if (preset.TopPEnabled)        body["top_p"]        = preset.TopP;
            if (preset.FrequencyPenaltyEnabled) body["frequency_penalty"] = preset.FrequencyPenalty;
            if (preset.PresencePenaltyEnabled)  body["presence_penalty"]  = preset.PresencePenalty;
            if (preset.SeedEnabled && preset.Seed >= 0) body["seed"] = preset.Seed;
            return body;
        }

        private static JObject BuildKoboldBody(TextGenPreset preset, string prompt)
        {
            var body = new JObject { ["prompt"] = prompt };
            if (preset.MaxNewTokens.HasValue) body["max_length"] = preset.MaxNewTokens.Value;
            body["temperature"] = preset.Temperature;
            body["top_p"] = preset.TopP;
            body["top_k"] = preset.TopK;
            body["rep_pen"] = preset.RepPen;
            body["rep_pen_range"] = preset.RepPenRange;
            body["min_p"] = preset.MinP;
            return body;
        }

        private static JObject BuildLlamaCppBody(TextGenPreset preset, string prompt)
        {
            var body = new JObject { ["prompt"] = prompt };
            if (preset.MaxNewTokens.HasValue) body["n_predict"] = preset.MaxNewTokens.Value;
            body["temperature"] = preset.Temperature;
            body["top_p"] = preset.TopP;
            body["top_k"] = preset.TopK;
            body["repeat_penalty"] = preset.RepPen;
            body["min_p"] = preset.MinP;
            return body;
        }

        private static JObject BuildTextCompletionBody(TextGenPreset preset, string prompt)
        {
            var body = new JObject { ["prompt"] = prompt };
            if (preset.MaxNewTokens.HasValue) body["max_tokens"] = preset.MaxNewTokens.Value;
            body["temperature"] = preset.Temperature;
            body["top_p"] = preset.TopP;
            body["frequency_penalty"] = preset.FrequencyPenalty;
            body["presence_penalty"] = preset.PresencePenalty;
            return body;
        }
    }

    public class ConnectionTestResult
    {
        public bool Connected { get; set; }
        public string Model   { get; set; }
        public string Error   { get; set; }
    }
}
