using Engine.Core;

using Game;
using Game.Network;
using Game.Network.Packages;

namespace Survivalcraft.Test.Network;

public class NetNodeSnapshotCoalescingTest
{
    [Fact]
    public void PlayerSnapshotsPreserveMissingFieldsAndPreferNewestValues()
    {
        var packages = new List<IPackage>
        {
            new ComponentPlayerPackage
            {
                FromPlayerId = 7,
                Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
                PackageChangeFlag = ComponentPlayerPackage.ChangFlag.PositionChange |
                                    ComponentPlayerPackage.ChangFlag.VelocityChange,
                Position = new Vector3(1f, 2f, 3f),
                Velocity = new Vector3(4f, 5f, 6f)
            }
        };
        var newer = new ComponentPlayerPackage
        {
            FromPlayerId = 7,
            Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
            PackageChangeFlag = ComponentPlayerPackage.ChangFlag.VelocityChange,
            Velocity = new Vector3(7f, 8f, 9f)
        };
        Assert.True(SnapshotPackageCoalescer.TryCoalesce(packages, newer));

        var package = Assert.IsType<ComponentPlayerPackage>(Assert.Single(packages));
        Assert.True(package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.PositionChange));
        Assert.True(package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.VelocityChange));
        Assert.Equal(new Vector3(1f, 2f, 3f), package.Position);
        Assert.Equal(new Vector3(7f, 8f, 9f), package.Velocity);
    }

    [Fact]
    public void BodySnapshotsAreNotCoalescedToKeepIndependentPackages()
    {
        var packages = new List<IPackage>();
        var older = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
        };
        older.BodyList.Add(new SubsystemBodyPackage.BodyItem { CreatureId = 1 });

        var newer = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
        };
        newer.BodyList.Add(new SubsystemBodyPackage.BodyItem { CreatureId = 2 });

        packages.Add(older);
        // 生物快照按独立小包发送，不允许合并器吃掉其它分块。
        Assert.False(SnapshotPackageCoalescer.TryCoalesce(packages, newer));
        Assert.Single(packages);
        Assert.Same(older, packages[0]);
    }

    [Fact]
    public void PickableSnapshotsUseLatestCompleteList()
    {
        var packages = new List<IPackage>();
        var older = new PickablePackage
        {
            Type = PickablePackage.PickType.Update
        };
        older.Pickables.Add(new Pickable { Id = 1, Position = new Vector3(1f, 2f, 3f) });

        var newer = new PickablePackage
        {
            Type = PickablePackage.PickType.Update
        };
        newer.Pickables.Add(new Pickable { Id = 2, Position = new Vector3(4f, 5f, 6f) });

        packages.Add(older);
        Assert.True(SnapshotPackageCoalescer.TryCoalesce(packages, newer));

        var package = Assert.IsType<PickablePackage>(Assert.Single(packages));
        Assert.Equal((ushort)2, Assert.Single(package.Pickables).Id);
    }
}
