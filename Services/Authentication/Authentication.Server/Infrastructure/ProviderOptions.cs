namespace SCNET.Authentication.Server.Infrastructure;

public sealed class ProviderOptions
{
    public string PublicUrl { get; set; } = string.Empty;

    public string DisplayName { get; set; } = "SCNET Authentication";

    public string DataDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "AuthenticationData");

    public Dictionary<string, string> Accounts { get; set; } = new(StringComparer.Ordinal);

    public string[] TrustedProxies { get; set; } = ["127.0.0.1", "::1"];
}
