using System;
using System.Collections.Generic;

namespace PocketTavern.UWP.Models
{
    public class AvailableModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int? ContextLength { get; set; }
    }

    public class ApiConfiguration
    {
        public string MainApi { get; set; } = "textgenerationwebui";
        public string TextGenType { get; set; } = "koboldcpp";
        public string ApiServer { get; set; } = "http://127.0.0.1:5001";
        public string ChatCompletionSource { get; set; } = "openai";
        public string CustomUrl { get; set; }
        public string ApiKey { get; set; } = "";
        public string CurrentModel { get; set; } = "";
        public List<AvailableModel> AvailableModels { get; set; } = new List<AvailableModel>();
        public bool IsConnected { get; set; } = false;
        public string ConnectionError { get; set; }

        public bool UsesChatCompletions =>
            string.Equals(MainApi, "openai", StringComparison.OrdinalIgnoreCase);

        public string ChatCompletionBaseUrl
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomUrl))
                    return CustomUrl.TrimEnd('/');
                switch (ChatCompletionSource?.ToLowerInvariant())
                {
                    case "openai":       return "https://api.openai.com";
                    case "nanogpt":      return "https://nano-gpt.com/api";
                    case "openrouter":   return "https://openrouter.ai/api";
                    case "deepseek":     return "https://api.deepseek.com";
                    case "mistralai":    return "https://api.mistral.ai";
                    case "cohere":       return "https://api.cohere.ai/v1";
                    case "perplexity":   return "https://api.perplexity.ai";
                    case "groq":         return "https://api.groq.com/openai";
                    case "makersuite":   return "https://generativelanguage.googleapis.com/v1beta/openai";
                    case "ai21":         return "https://api.ai21.com/studio/v1";
                    case "xai":          return "https://api.x.ai";
                    case "fireworks":    return "https://api.fireworks.ai/inference";
                    case "moonshot":     return "https://api.moonshot.cn";
                    case "aimlapi":      return "https://api.aimlapi.com";
                    case "pollinations": return "https://text.pollinations.ai/openai";
                    case "chutes":       return "https://llm.chutes.ai";
                    case "electronhub":  return "https://api.electronhub.top";
                    case "siliconflow":  return "https://api.siliconflow.cn";
                    case "zai":          return "https://api.z.ai";
                    case "claude":       return "https://api.anthropic.com";
                    default:             return CustomUrl?.TrimEnd('/') ?? "";
                }
            }
        }

        public string DisplayName => UsesChatCompletions
            ? ChatCompletionSourceDisplayName(ChatCompletionSource)
            : TextGenTypeDisplayName(TextGenType);

        public static ApiConfiguration Default => new ApiConfiguration();

        public static string TextGenTypeDisplayName(string type)
        {
            switch (type?.ToLowerInvariant())
            {
                case "koboldcpp":    return "KoboldCpp";
                case "llamacpp":     return "llama.cpp";
                case "ooba":         return "Text Gen WebUI";
                case "vllm":         return "vLLM";
                case "aphrodite":    return "Aphrodite";
                case "tabby":        return "TabbyAPI";
                case "ollama":       return "Ollama";
                case "togetherai":   return "Together AI";
                case "infermaticai": return "Infermatic AI";
                case "openrouter":   return "OpenRouter";
                case "featherless":  return "Featherless";
                case "mancer":       return "Mancer";
                case "dreamgen":     return "DreamGen";
                case "huggingface":  return "HuggingFace";
                case "generic":      return "Generic";
                default:             return type ?? "";
            }
        }

        public static string ChatCompletionSourceDisplayName(string source)
        {
            switch (source?.ToLowerInvariant())
            {
                case "openai":       return "OpenAI";
                case "claude":       return "Claude";
                case "openrouter":   return "OpenRouter";
                case "nanogpt":      return "NanoGPT";
                case "deepseek":     return "DeepSeek";
                case "mistralai":    return "Mistral AI";
                case "cohere":       return "Cohere";
                case "perplexity":   return "Perplexity";
                case "groq":         return "Groq";
                case "makersuite":   return "Google AI Studio";
                case "vertexai":     return "Vertex AI";
                case "ai21":         return "AI21";
                case "xai":          return "xAI (Grok)";
                case "fireworks":    return "Fireworks";
                case "moonshot":     return "Moonshot";
                case "aimlapi":      return "AIML API";
                case "pollinations": return "Pollinations";
                case "chutes":       return "Chutes";
                case "electronhub":  return "ElectronHub";
                case "siliconflow":  return "SiliconFlow";
                case "zai":          return "Z.AI";
                case "azure_openai": return "Azure OpenAI";
                case "custom":       return "Custom";
                default:             return source ?? "";
            }
        }
    }
}
