using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemSeasonPackage : IPackage
{
    public float TimeOfYear { get; set; }

    public byte ID => (byte)PackageType.SubsystemSeason;




    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public SubsystemSeasonPackage()
    {
    }

    public SubsystemSeasonPackage(float timeOfYear)
    {
        TimeOfYear = timeOfYear;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(TimeOfYear);
    }

    public void ReadData(PackageStreamReader reader)
    {
        TimeOfYear = reader.ReadSingle();
    }
}
