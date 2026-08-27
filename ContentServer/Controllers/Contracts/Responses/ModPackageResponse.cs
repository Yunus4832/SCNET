namespace ContentServer.Controllers.Contracts.Responses;

public sealed record ModPackageResponse(
    string ModId,
    string Version,
    string PackageHash,
    string FileName,
    long PackageSize,
    string Side,
    string? Description,
    DateTimeOffset UploadedAtUtc,
    string DownloadUrl);
