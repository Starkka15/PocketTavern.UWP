namespace PocketTavern.UWP.Models
{
    public class ServerSettings
    {
        public string ForgeUrl { get; set; } = "";
        public string ProxyUrl { get; set; } = "";
        public string CharaVaultUrl { get; set; } = "";
        public string CharavaultMode { get; set; } = "local";   // "local" or "charavault"

        public string NormalizedForgeUrl => ForgeUrl?.TrimEnd('/') ?? "";
        public string NormalizedProxyUrl => ProxyUrl?.TrimEnd('/') ?? "";
        public string NormalizedCharaVaultUrl => CharaVaultUrl?.TrimEnd('/') ?? "";
        public bool IsCharaVaultEnabled => !string.IsNullOrWhiteSpace(CharaVaultUrl);
        public bool IsForgeEnabled => !string.IsNullOrWhiteSpace(ForgeUrl);
    }
}
