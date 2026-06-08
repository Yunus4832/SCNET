namespace Game.Network.Packages.Handlers;

public sealed class TerritoriyPackageHandler : PackageHandlerBase<TerritoriyPackage>
{
    public override void Handle(TerritoriyPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(TerritoriyPackage)}");
            return;
        }

        if (!SubsystemBedrockBlockBehavior.Territories.TryGetValue(package.Guid, out var territoriy))
        {
            return;
        }

        territoriy.AllowDig = package.AllowDig;
        territoriy.AllowPlace = package.AllowPlace;
        territoriy.ApplyToFriend = package.ApplyToFriend;
        territoriy.IsVisible = package.IsVisible;
        if (!isServer)
        {
            return;
        }

        package.Except = package.From;
        netNode.QueuePackage(package);
    }
}
