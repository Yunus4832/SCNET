using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record SoundGeneratorNotePackage : IProtocolMessage
{
    [Key(0)] public SoundGeneratorNote Note { get; init; }
}
