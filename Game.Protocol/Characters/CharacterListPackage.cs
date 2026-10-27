using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterListPackage() : IProtocolMessage
{
    [Key(0)] public IReadOnlyList<CharacterListEntry> Characters { get; init; } = [];

    public CharacterListPackage(IEnumerable<CharacterListEntry> players) : this() => Characters = players.ToArray();
}
