namespace Game.Protocol.Players;

public sealed record OnlinePlayerStatePackage : IProtocolMessage
{
    public IReadOnlyList<OnlinePlayerState> Players { get; init; } = [];
    public byte ID => (byte)PackageType.OnlinePlayerState;
    public OnlinePlayerStatePackage() { }
    public OnlinePlayerStatePackage(IEnumerable<OnlinePlayerState> players) => Players = players.ToArray();
}
