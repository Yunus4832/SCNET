namespace Game.Protocol.Players;

public sealed record PlayerListPackage : IProtocolMessage
{
    public IReadOnlyList<PlayerListEntry> Players { get; init; } = [];
    public byte ID => (byte)PackageType.PlayerList;
    public PlayerListPackage() { }
    public PlayerListPackage(IEnumerable<PlayerListEntry> players) => Players = players.ToArray();
}
