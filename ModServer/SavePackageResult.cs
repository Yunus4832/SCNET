namespace ModServer;

public enum SavePackageStatus
{
    Created,
    Unchanged,
    Conflict
}

public sealed record SavePackageResult(SavePackageStatus Status, ModPackageRecord? Record);
