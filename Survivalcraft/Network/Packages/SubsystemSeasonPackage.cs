using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemSeasonPackage : IPackage
{
    public int SeasonIndexNet;

    public float TimeOfSeasonNet { get; set; }

    public byte ID => (byte)PackageType.SubsystemSeason;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public SubsystemSeasonPackage()
    {
    }

    public SubsystemSeasonPackage(int seasonIndex, float timeOfSeason)
    {
        SeasonIndexNet = seasonIndex; //季节编号
        TimeOfSeasonNet = timeOfSeason;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(SeasonIndexNet);
        writer.Write(BitConverter.ToInt32(BitConverter.GetBytes(TimeOfSeasonNet), 0));
    }

    public void ReadData(PackageStreamReader reader)
    {
        SeasonIndexNet = reader.ReadInt32();
        TimeOfSeasonNet = BitConverter.ToSingle(BitConverter.GetBytes(reader.ReadInt32()), 0);
    }


}
