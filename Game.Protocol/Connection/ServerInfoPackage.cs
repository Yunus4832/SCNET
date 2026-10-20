namespace Game.Protocol.Connection;

public sealed record ServerInfoPackage : IProtocolMessage
{
    public bool RequestInfo { get; init; }
    public int ProtocolVersion { get; init; }
    public ushort ClientCount { get; init; }
    public ushort MaxPlayerCount { get; init; }
    public byte GameMode { get; init; }
    public float TimeOfDay { get; init; }
    public byte Season { get; init; }
    public float TimeOfSeason { get; init; }
    public IReadOnlyList<ContentRepositoryDescriptor> TemporaryRepositories { get; init; } = [];
    public RequiredModProfile? RequiredModProfile { get; init; }
    public byte ID => (byte)PackageType.ServerInfo;

    public ServerInfoPackage() { }
    public ServerInfoPackage(bool requestInfo) => RequestInfo = requestInfo;
}
