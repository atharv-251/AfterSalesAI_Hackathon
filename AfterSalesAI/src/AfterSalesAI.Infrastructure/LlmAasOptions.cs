namespace AfterSalesAI.Infrastructure;

public sealed class LlmAasOptions
{
    public const string SectionName = "LlmAas";
    public string BaseUrl { get; set; } = "https://llmapi.ai.vwgroup.com";
    public string Model { get; set; } = "smart-router";
    public string TokenEndpoint { get; set; } = "https://idp.cloud.vwgroup.com/auth/realms/kums-mfa/protocol/openid-connect/token";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string VirtualKey { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 4096;

    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)
        && Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(VirtualKey);
}
