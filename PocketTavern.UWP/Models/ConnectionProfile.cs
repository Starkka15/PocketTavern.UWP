using System;

namespace PocketTavern.UWP.Models
{
    public class ConnectionProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string MainApi { get; set; } = "textgenerationwebui";
        public string TextGenType { get; set; } = "koboldcpp";
        public string ApiServer { get; set; } = "http://127.0.0.1:5001";
        public string ChatCompletionSource { get; set; } = "openai";
        public string CustomUrl { get; set; }
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public string TextGenPreset { get; set; } = "";
        public string InstructPreset { get; set; } = "";
        public string ContextPreset { get; set; } = "";
        public string SyspromptPreset { get; set; } = "";

        public string ApiLabel => string.Equals(MainApi, "openai", StringComparison.OrdinalIgnoreCase)
            ? ApiConfiguration.ChatCompletionSourceDisplayName(ChatCompletionSource)
            : ApiConfiguration.TextGenTypeDisplayName(TextGenType);
    }
}
