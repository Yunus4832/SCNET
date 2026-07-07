namespace ModServer;

public enum SavePackageStatus
{
    Created,
    Unchanged,
    Replaced,
    Conflict
}

public sealed record SavePackageResult(SavePackageStatus Status, ModPackageRecord? Record);
