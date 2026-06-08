using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemSeasonPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var weather = project.FindSubsystem<SubsystemSeasons>(true)!;
        weather.Season = (Season)SeasonIndexNet;
        weather.TimeOfSeason = TimeOfSeasonNet;
    }
}

public sealed class SubsystemSeasonPackageHandler : PackageHandlerBase<SubsystemSeasonPackage>
{
    public override void Handle(SubsystemSeasonPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemSeasonPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
