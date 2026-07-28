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
            case SubsystemBodyPackage
            {
                PackageEventType: SubsystemBodyPackage.EventType.BodyUpdate
            }:
                key = new SnapshotKey(typeof(SubsystemBodyPackage), 0, package.To, package.Except);
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
            case (SubsystemBodyPackage olderBodies, SubsystemBodyPackage newerBodies):
                MergeBodies(olderBodies, newerBodies);
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

    private static void MergeBodies(SubsystemBodyPackage older, SubsystemBodyPackage newer)
    {
        var olderItems = older.BodyList.ToDictionary(item => item.CreatureId);
        for (var i = 0; i < newer.BodyList.Count; i++)
        {
            var newerItem = newer.BodyList[i];
            if (!olderItems.TryGetValue(newerItem.CreatureId, out var olderItem))
            {
                continue;
            }

            if (HasOlderValue(olderItem, newerItem, SubsystemBodyPackage.ChangeFlag.LookAnglesChange))
            {
                newerItem.LookAngles = olderItem.LookAngles;
            }

            if (HasOlderValue(olderItem, newerItem, SubsystemBodyPackage.ChangeFlag.FlyOrderChange))
            {
                newerItem.FlyOrder = olderItem.FlyOrder;
            }

            if (HasOlderValue(olderItem, newerItem, SubsystemBodyPackage.ChangeFlag.PositionChange))
            {
                newerItem.Position = olderItem.Position;
            }

            if (HasOlderValue(olderItem, newerItem, SubsystemBodyPackage.ChangeFlag.RotationChange))
            {
                newerItem.Rotation = olderItem.Rotation;
            }

            if (HasOlderValue(olderItem, newerItem, SubsystemBodyPackage.ChangeFlag.VelocityChange))
            {
                newerItem.Velocity = olderItem.Velocity;
            }

            newerItem.ChangeFlag |= olderItem.ChangeFlag;
            newer.BodyList[i] = newerItem;
        }
    }

    private static bool HasOlderValue(
        SubsystemBodyPackage.BodyItem older,
        SubsystemBodyPackage.BodyItem newer,
        SubsystemBodyPackage.ChangeFlag flag)
    {
        return older.ChangeFlag.HasFlag(flag) && !newer.ChangeFlag.HasFlag(flag);
    }

    private readonly record struct SnapshotKey(Type Type, int EntityId, Client? To, Client? Except);
}
