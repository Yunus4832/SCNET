using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemSkyPackage : IPackage
{
    public bool IsRequest;

    public Vector3 LightningStrikePosition;

    public Vector3 Direction;

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

    public SubsystemSkyPackage(Vector3 position, Vector3 direction)
    {
        LightningStrikePosition = position;
        Direction = direction;
        IsRequest = true;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(LightningStrikePosition);
        writer.Write(IsRequest);
        writer.Write(Direction);
    }

    public void ReadData(PackageStreamReader reader)
    {
        LightningStrikePosition = reader.ReadVector3();
        IsRequest = reader.ReadBoolean();
        Direction = reader.ReadVector3();
    }


}
