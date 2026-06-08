using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemTimePackage : IPackage
{
    public double Time;

    public double TimeOfDayOffset;

    public byte ID => (byte)PackageType.SubsystemTime;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;


    public SubsystemTimePackage()
    {
    }

    public SubsystemTimePackage(double totalElapsedGameTime, double timeOfDayOffset)
    {
        Time = totalElapsedGameTime;
        TimeOfDayOffset = timeOfDayOffset;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Time);
        writer.Write(TimeOfDayOffset);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Time = reader.ReadDouble();
        TimeOfDayOffset = reader.ReadDouble();
    }


}
