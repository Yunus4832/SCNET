namespace WorldUpgradeTool.Core;

internal sealed class WorldInspection
{
    public required string DirectoryName { get; init; }

    public required bool DirectoryExists { get; init; }

    public required bool HasProjectXml { get; init; }

    public required bool HasProjectJson { get; init; }

    public required bool HasRegionsDirectory { get; init; }

    public string ProjectVersion { get; init; } = "<unknown>";

    public string WorldName { get; init; } = "<unnamed>";

    public bool IsTargetProjectVersion => ProjectVersion == WorldUpgradeManager.TargetProjectFormatVersion;

    public bool IsTerrainStorageTargetVersion => ProjectVersion == WorldUpgradeManager.TargetTerrainStorageVersion;
}
