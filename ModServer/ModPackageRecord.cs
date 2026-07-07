namespace ModServer;

public sealed record ModPackageRecord(
    string ModId,
    string Version,
    string PackageHash,
    string FileName,
    long PackageSize,
    string Side,
    string? Description,
    DateTimeOffset UploadedAtUtc
);
