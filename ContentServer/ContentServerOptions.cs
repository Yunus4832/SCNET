namespace ContentServer;

public sealed class ContentServerOptions
{
    public const string SectionName = "ContentServer";

    public string DatabasePath { get; set; } = "Data/content-server.db";

    public long MaximumPackageBytes { get; set; } = 256L * 1024L * 1024L;

    public string[] AllowedOrigins { get; set; } = [];
}
