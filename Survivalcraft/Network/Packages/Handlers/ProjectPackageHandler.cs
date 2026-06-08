using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ProjectPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        var loadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!;
        loadingScreen.ReplyCall(HasTexture, TextureData, ProjectData);
    }
}

public sealed class ProjectPackageHandler : PackageHandlerBase<ProjectPackage>
{
    public override void Handle(ProjectPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ProjectPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
