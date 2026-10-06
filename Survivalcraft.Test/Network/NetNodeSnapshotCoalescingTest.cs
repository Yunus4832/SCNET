using Engine.Core;

using Game;
using Game.Network;
using Game.Network.Packages;

namespace Survivalcraft.Test.Network;

public class NetNodeSnapshotCoalescingTest
{
    [Fact]
    public void PlayerSnapshotsReplaceOldStateWithoutRetainingOldOptionalFields()
    {
        var packages = new List<OutboundPackage>
        {
            new(
                new ComponentPlayerPackage
                {
                    FromPlayerId = 7,
                    Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
                    PackageChangeFlag = ComponentPlayerPackage.ChangFlag.PositionChange |
                                        ComponentPlayerPackage.ChangFlag.VelocityChange |
                                        ComponentPlayerPackage.ChangFlag.LadderChange,
                    LadderValue = 10,
                    Position = new Vector3(1f, 2f, 3f),
                    Velocity = new Vector3(4f, 5f, 6f)
                },
                PackageAudience.Global)
        };
        var newer = new ComponentPlayerPackage
        {
            FromPlayerId = 7,
            Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
            PackageChangeFlag = ComponentPlayerPackage.ChangFlag.PositionChange |
                                ComponentPlayerPackage.ChangFlag.VelocityChange,
            Position = new Vector3(10f, 11f, 12f),
            Velocity = new Vector3(7f, 8f, 9f)
        };
        Assert.True(SnapshotPackageCoalescer.TryCoalesce(
            packages,
            new OutboundPackage(newer, PackageAudience.Global)));

        var package = Assert.IsType<ComponentPlayerPackage>(Assert.Single(packages).Package);
        Assert.True(package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.PositionChange));
        Assert.True(package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.VelocityChange));
        Assert.Same(newer, package);
        Assert.False(package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LadderChange));
        Assert.Equal(new Vector3(10f, 11f, 12f), package.Position);
        Assert.Equal(new Vector3(7f, 8f, 9f), package.Velocity);
    }

    [Fact]
    public void BodySnapshotsAreNotCoalescedToKeepIndependentPackages()
    {
        var packages = new List<OutboundPackage>();
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

        packages.Add(new OutboundPackage(older, PackageAudience.Global));
        // 生物快照按独立小包发送，不允许合并器吃掉其它分块。
        Assert.False(SnapshotPackageCoalescer.TryCoalesce(
            packages,
            new OutboundPackage(newer, PackageAudience.Global)));
        Assert.Single(packages);
        Assert.Same(older, packages[0].Package);
    }

    [Fact]
    public void PickableSnapshotChunksAreNotCoalesced()
    {
        var packages = new List<OutboundPackage>();
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

        packages.Add(new OutboundPackage(older, PackageAudience.Global));
        Assert.False(SnapshotPackageCoalescer.TryCoalesce(
            packages,
            new OutboundPackage(newer, PackageAudience.Global)));

        var package = Assert.IsType<PickablePackage>(Assert.Single(packages).Package);
        Assert.Equal((ushort)1, Assert.Single(package.Pickables).Id);
    }

    [Fact]
    public void SnapshotsForDifferentAudiencesAreNotCoalesced()
    {
        var firstClient = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var secondClient = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        var packages = new List<OutboundPackage>
        {
            new(
                new OnlinePlayerStatePackage(),
                PackageAudience.To(firstClient))
        };

        Assert.False(SnapshotPackageCoalescer.TryCoalesce(
            packages,
            new OutboundPackage(
                new OnlinePlayerStatePackage(),
                PackageAudience.To(secondClient))));
        Assert.Single(packages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotsDoNotMergeAcrossReusedConnectionIds(bool exclusion)
    {
        var oldClient = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var newClient = new Client(null, 1, Guid.NewGuid(), oldClient.GUID, null);
        var packages = new List<OutboundPackage>
        {
            new(new OnlinePlayerStatePackage(),
                exclusion ? PackageAudience.Except(oldClient) : PackageAudience.To(oldClient))
        };

        Assert.False(SnapshotPackageCoalescer.TryCoalesce(packages,
            new OutboundPackage(new OnlinePlayerStatePackage(),
                exclusion ? PackageAudience.Except(newClient) : PackageAudience.To(newClient))));
    }

    [Fact]
    public void EquivalentRecipientSetsCoalesceRegardlessOfOrderOrDuplicates()
    {
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var second = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        var older = new OnlinePlayerStatePackage();
        var newer = new OnlinePlayerStatePackage();
        var packages = new List<OutboundPackage> { new(older, PackageAudience.To([first, second])) };

        Assert.True(SnapshotPackageCoalescer.TryCoalesce(packages,
            new OutboundPackage(newer, PackageAudience.To([second, first, first]))));
        Assert.Same(newer, Assert.Single(packages).Package);
    }
}
