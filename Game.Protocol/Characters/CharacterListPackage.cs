namespace Game.Protocol.Characters;

public sealed record CharacterListPackage : IProtocolMessage
{
    public IReadOnlyList<CharacterListEntry> Characters { get; init; } = [];

    public byte ID => (byte)PackageType.CharacterList;

    public CharacterListPackage()
    {
    }

    public CharacterListPackage(IEnumerable<CharacterListEntry> players) => Characters = players.ToArray();
}
