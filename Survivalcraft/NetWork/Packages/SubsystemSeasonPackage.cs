namespace Game.NetWork.Packages;

public class SubsystemSeasonPackage : IPackage
{
    private int _seasonIndexNet;

    public Season SeasonNet { get; set; }

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
        _seasonIndexNet = seasonIndex; //季节编号
        TimeOfSeasonNet = timeOfSeason;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_seasonIndexNet);
        writer.Write(BitConverter.ToInt32(BitConverter.GetBytes(TimeOfSeasonNet), 0));
    }

    public void ReadData(PackageStreamReader reader)
    {
        _seasonIndexNet = reader.ReadInt32();
        TimeOfSeasonNet = BitConverter.ToSingle(BitConverter.GetBytes(reader.ReadInt32()), 0);
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        var weather = projectNet.FindSubsystem<SubsystemSeasons>(true)!;
        weather.Season = (Season)_seasonIndexNet;
        weather.TimeOfSeason = TimeOfSeasonNet;
    }
}
