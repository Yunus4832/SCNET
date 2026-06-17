namespace Game.Modding;

public sealed class ModRepositoryPackage
{
    public string ModId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string PackageHash { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long PackageSize { get; set; }

    public string Side { get; set; } = "common";

    public string? Description { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }

    public string DownloadUrl { get; set; } = string.Empty;
}
