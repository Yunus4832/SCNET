using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemSkyPackage : IPackage
{
    public Vector3 LightningStrikePosition;

    public byte ID => (byte)PackageType.SubsystemSky;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    public SubsystemSkyPackage()
    {
    }

    public SubsystemSkyPackage(Vector3 position)
    {
        LightningStrikePosition = position;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(LightningStrikePosition);
    }

    public void ReadData(PackageStreamReader reader)
    {
        LightningStrikePosition = reader.ReadVector3();
    }


}
