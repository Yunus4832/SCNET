namespace Game.Network.Packages.Handlers;

public sealed class TerritoriyPackageHandler : PackageHandlerBase<TerritoriyPackage>
{
    internal static bool CanApplyRequest(Client? sender, Guid ownerGuid)
    {
        return sender is not null && ownerGuid != Guid.Empty && sender.GUID == ownerGuid;
    }

    public override void Handle(TerritoriyPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(TerritoriyPackage)}");
            return;
        }

        if (!SubsystemTerritoryBlockBehavior.Territories.TryGetValue(package.Guid, out var territoriy))
        {
            return;
        }

        if (isServer && !CanApplyRequest(context.Sender, territoriy.OwnerGuid))
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

        netNode.QueuePackage(
            package,
            PackageAudience.Except(context.Sender!));
    }
}
