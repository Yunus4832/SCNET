namespace Game.Network.Packages.Handlers;

public sealed class SubsystemTimePackageHandler : PackageHandlerBase<SubsystemTimePackage>
{
    public override void Handle(SubsystemTimePackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var info = project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (info.WorldSettings.GameMode == GameMode.Creative || !isServer)
        {
            info.TotalElapsedGameTime = package.Time;
            info.TimeOfDay.TimeOfDayOffset = package.TimeOfDayOffset;
        }
        else
        {
            if (package.From != null)
            {
                Log.Information($"{package.From.PlayerData.Name} 打算在非创造模式下修改时间");
            }
        }
    }
}
