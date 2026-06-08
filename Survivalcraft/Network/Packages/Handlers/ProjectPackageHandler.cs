namespace Game.Network.Packages.Handlers;

public sealed class ProjectPackageHandler : PackageHandlerBase<ProjectPackage>
{
    public override void Handle(ProjectPackage package, NetNode? netNode, bool isServer)
    {
        var loadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!;
        loadingScreen.ReplyCall(package.HasTexture, package.TextureData, package.ProjectData);
    }
}
