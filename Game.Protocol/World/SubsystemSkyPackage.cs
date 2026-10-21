using Engine.Core.Mathematics;

namespace Game.Protocol.World;

public sealed record SubsystemSkyPackage(Vector3 LightningStrikePosition) : IProtocolMessage
{
    public byte ID => (byte)PackageType.SubsystemSky;
}
