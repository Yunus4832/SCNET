using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class TerritoriyPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (!SubsystemBedrockBlockBehavior.Territories.TryGetValue(Guid, out var territoriy))
        {
            return;
        }

        territoriy.AllowDig = AllowDig;
        territoriy.AllowPlace = AllowPlace;
        territoriy.ApplyToFriend = ApplyToFriend;
        territoriy.IsVisible = IsVisible;
        if (!isServer)
        {
            return;
        }

        Except = From;
        netNode.QueuePackage(this);
    }
}

public sealed class TerritoriyPackageHandler : PackageHandlerBase<TerritoriyPackage>
{
    public override void Handle(TerritoriyPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(TerritoriyPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
