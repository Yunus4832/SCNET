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
    public void BodySnapshotsUseLatestMembershipAndPreservePendingChanges()
    {
        var packages = new List<IPackage>();
        var older = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
        };
        older.BodyList.Add(new SubsystemBodyPackage.BodyItem
        {
            CreatureId = 1,
            ChangeFlag = SubsystemBodyPackage.ChangeFlag.PositionChange,
            Position = new Vector3(1f, 2f, 3f)
        });
        older.BodyList.Add(new SubsystemBodyPackage.BodyItem { CreatureId = 2 });

        var newer = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
        };
        newer.BodyList.Add(new SubsystemBodyPackage.BodyItem
        {
            CreatureId = 1,
            ChangeFlag = SubsystemBodyPackage.ChangeFlag.VelocityChange,
            Velocity = new Vector3(4f, 5f, 6f)
        });
        newer.BodyList.Add(new SubsystemBodyPackage.BodyItem { CreatureId = 3 });

        packages.Add(older);
        Assert.True(SnapshotPackageCoalescer.TryCoalesce(packages, newer));

        var package = Assert.IsType<SubsystemBodyPackage>(Assert.Single(packages));
        Assert.Equal([1, 3], package.BodyList.Select(item => item.CreatureId));
        var item = package.BodyList[0];
        Assert.True(item.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.PositionChange));
        Assert.True(item.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.VelocityChange));
        Assert.Equal(new Vector3(1f, 2f, 3f), item.Position);
        Assert.Equal(new Vector3(4f, 5f, 6f), item.Velocity);
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
