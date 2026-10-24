namespace Game.Protocol.Characters;

public sealed record OnlineCharacterStatePackage : IProtocolMessage
{
    public IReadOnlyList<OnlineCharacterState> Characters { get; init; } = [];

    public byte ID => (byte)PackageType.OnlineCharacterState;

    public OnlineCharacterStatePackage()
    {
    }

    public OnlineCharacterStatePackage(IEnumerable<OnlineCharacterState> players) => Characters = players.ToArray();
}
