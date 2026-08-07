using Game.Network.Packages;

namespace Game.Network;

public static class SnapshotPackageCoalescer
{
    public static bool TryCoalesce(List<IPackage> pendingPackages, IPackage newer)
    {
        if (!TryGetKey(newer, out var key))
        {
            return false;
        }

        for (var i = pendingPackages.Count - 1; i >= 0; i--)
        {
            if (!TryGetKey(pendingPackages[i], out var pendingKey) || pendingKey != key)
            {
                continue;
            }

            pendingPackages[i] = Merge(pendingPackages[i], newer);
            return true;
        }

        return false;
    }

    private static bool TryGetKey(IPackage package, out SnapshotKey key)
    {
        switch (package)
        {
            case ComponentPlayerPackage
            {
                Type: ComponentPlayerPackage.PlayerAction.BodyUpdate
            } playerPackage:
                key = new SnapshotKey(
                    typeof(ComponentPlayerPackage),
                    playerPackage.PlayerData?.ClientId ?? playerPackage.FromPlayerId,
                    package.To,
                    package.Except);
                return true;
            case PickablePackage { Type: PickablePackage.PickType.Update }:
                key = new SnapshotKey(typeof(PickablePackage), 0, package.To, package.Except);
                return true;
            case OnlinePlayerStatePackage:
                key = new SnapshotKey(typeof(OnlinePlayerStatePackage), 0, package.To, package.Except);
                return true;
            default:
                key = default;
                return false;
        }
    }

    private static IPackage Merge(IPackage older, IPackage newer)
    {
        switch (older, newer)
        {
            case (ComponentPlayerPackage olderPlayer, ComponentPlayerPackage newerPlayer):
                MergePlayer(olderPlayer, newerPlayer);
                break;
        }

        return newer;
    }

    private static void MergePlayer(ComponentPlayerPackage older, ComponentPlayerPackage newer)
    {
        var parentFlag = ComponentPlayerPackage.ChangFlag.ParentBodyChange;
        if (older.PackageChangeFlag.HasFlag(parentFlag) != newer.PackageChangeFlag.HasFlag(parentFlag))
        {
            return;
        }

        CopyPlayerValue(older, newer, ComponentPlayerPackage.ChangFlag.LookAnglesChange,
            static (source, target) => target.LookAngles = source.LookAngles);
        CopyPlayerValue(older, newer, ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange,
            static (source, target) => target.ChildLookAngles = source.ChildLookAngles);
        CopyPlayerValue(older, newer, ComponentPlayerPackage.ChangFlag.PositionChange,
            static (source, target) => target.Position = source.Position);
        CopyPlayerValue(older, newer, ComponentPlayerPackage.ChangFlag.RotationChange,
            static (source, target) => target.Rotation = source.Rotation);
        CopyPlayerValue(older, newer, ComponentPlayerPackage.ChangFlag.VelocityChange,
            static (source, target) => target.Velocity = source.Velocity);
        CopyPlayerValue(older, newer, ComponentPlayerPackage.ChangFlag.LadderChange,
            static (source, target) => target.LadderValue = source.LadderValue);
        CopyPlayerValue(older, newer, ComponentPlayerPackage.ChangFlag.SneakChange,
            static (source, target) => target.Sneaking = source.Sneaking);
        newer.PackageChangeFlag |= older.PackageChangeFlag;
    }

    private static void CopyPlayerValue(
        ComponentPlayerPackage older,
        ComponentPlayerPackage newer,
        ComponentPlayerPackage.ChangFlag flag,
        Action<ComponentPlayerPackage, ComponentPlayerPackage> copy)
    {
        if (older.PackageChangeFlag.HasFlag(flag) && !newer.PackageChangeFlag.HasFlag(flag))
        {
            copy(older, newer);
        }
    }

    private readonly record struct SnapshotKey(Type Type, int EntityId, Client? To, Client? Except);
}
