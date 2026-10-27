using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
[Union(0, typeof(CharacterCreateRejectedPackage))]
[Union(1, typeof(CharacterCreateRequestPackage))]
[Union(2, typeof(CharacterProfilePackage))]
[Union(3, typeof(CharacterUpdateLocationPackage))]
public abstract record CharacterDataPackage : IProtocolMessage
{
    public enum DataType
    {
        Create = 0,

        Modify = 1,

        CreationRejected = 2,

        // Tags 4–6 retired: unsent legacy notification modes.
        SetUpdateLocation = 3
    }

    [IgnoreMember] public abstract DataType Type { get; }
}
