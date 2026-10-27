using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record SubsystemSeasonPackage([property: Key(0)] float TimeOfYear) : IProtocolMessage
{
}
