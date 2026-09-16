namespace ContentServer;

public sealed class ContentServerOptions
{
    public const string SectionName = "ContentServer";

    public string DatabasePath { get; set; } = "Data/content-server.db";

    public string PackageStoragePath { get; set; } = "Data";

    public long MaximumPackageBytes { get; set; } = 256L * 1024L * 1024L;

    public string[] AllowedOrigins { get; set; } = [];

    public bool BuiltInServerDirectoryEnabled { get; set; } = true;

    public string BuiltInServerDirectoryId { get; set; } = "scnet-content-server";

    public string BuiltInServerDirectoryName { get; set; } = "SCNET Servers";

    public string BuiltInServerDirectoryDescription { get; set; } = "Server directory provided by this ContentServer";

    public string? PublicBaseUrl { get; set; }
}
