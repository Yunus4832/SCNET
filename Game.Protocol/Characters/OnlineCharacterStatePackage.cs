using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record OnlineCharacterStatePackage() : IProtocolMessage
{
    [Key(0)] public IReadOnlyList<OnlineCharacterState> Characters { get; init; } = [];

    public OnlineCharacterStatePackage(IEnumerable<OnlineCharacterState> players) : this() =>
        Characters = players.ToArray();
}
