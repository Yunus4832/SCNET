namespace Game.Protocol;

public static class ProtocolLimits
{
    public const int MaximumTemporaryRepositories = 16;

    public const int MaximumRepositoryUrlLength = 2048;

    public const int MinimumRepositoryPriority = 0;

    public const int MaximumRepositoryPriority = 1000;

    public const int MaximumRequiredMods = 256;

    public const int MaximumModProfileIdLength = 128;

    public const int MaximumModIdLength = 128;

    public const int MaximumModVersionLength = 64;

    public const int TerrainFragmentPayloadSize = 400;

    public const int MaximumTerrainPayloadLength = 2 * 1024 * 1024;

    public const int MaximumTerrainFragmentCount =
        (MaximumTerrainPayloadLength + TerrainFragmentPayloadSize - 1) / TerrainFragmentPayloadSize;

    public const int BlockCapacity = 1024;

    public const int MaximumResourceIdLength = 256;

    public const int MaximumPlayerEntitySnapshots = 1024;

    public const int MaximumCommandPayloadLength = 4096;
}
