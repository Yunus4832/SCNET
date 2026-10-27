using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record SubsystemSkyPackage([property: Key(0)] Vector3 LightningStrikePosition) : IProtocolMessage
{
}
