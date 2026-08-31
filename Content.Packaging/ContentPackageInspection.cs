namespace Content.Packaging;

public sealed record ContentPackageInspection(
    ContentPackageManifest Manifest,
    string PackageHash,
    IReadOnlyList<ContentPackageEntry> Entries
);

public sealed record ContentPackageEntry(string Path, long Length);
