using Engine.Core.Mathematics;

using Game.Network;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public class NetNodeSnapshotCoalescingTest
{
    [Fact]
    public void PlayerSnapshotsReplaceOldStateWithoutRetainingOldOptionalFields()
    {
        var packages = new List<OutboundPackage>
        {
            new(
                new CharacterStatePackage
                {
                    ControllerSessionId = 7,

                    PackageChangeFlag = ComponentCharacterPackage.ChangFlag.PositionChange |
                                        ComponentCharacterPackage.ChangFlag.VelocityChange |
                                        ComponentCharacterPackage.ChangFlag.LadderChange,
                    LadderValue = 10,
                    Position = new Vector3(1f, 2f, 3f),
                    Velocity = new Vector3(4f, 5f, 6f)
                },
                PackageAudience.Global
            )
        };
        var newer = new CharacterStatePackage
        {
            ControllerSessionId = 7,

            PackageChangeFlag = ComponentCharacterPackage.ChangFlag.PositionChange |
                                ComponentCharacterPackage.ChangFlag.VelocityChange,
            Position = new Vector3(10f, 11f, 12f),
            Velocity = new Vector3(7f, 8f, 9f)
        };
        Assert.True(
            SnapshotPackageCoalescer.TryCoalesce(
                packages,
                new OutboundPackage(newer, PackageAudience.Global)
            )
        );

        var package = Assert.IsType<CharacterStatePackage>(Assert.Single(packages).Package);
        Assert.True(package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.PositionChange));
        Assert.True(package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.VelocityChange));
        Assert.Same(newer, package);
        Assert.False(package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.LadderChange));
        Assert.Equal(new Vector3(10f, 11f, 12f), package.Position);
        Assert.Equal(new Vector3(7f, 8f, 9f), package.Velocity);
    }

    [Fact]
    public void BodySnapshotsAreNotCoalescedToKeepIndependentPackages()
    {
        var packages = new List<OutboundPackage>();
        var older = new BodyStatesPackage
            { BodyList = [new SubsystemBodyPackage.BodyItem { CreatureId = 1 }] };

        var newer = new BodyStatesPackage
            { BodyList = [new SubsystemBodyPackage.BodyItem { CreatureId = 2 }] };

        packages.Add(new OutboundPackage(older, PackageAudience.Global));
        // 生物快照按独立小包发送，不允许合并器吃掉其它分块。
        Assert.False(
            SnapshotPackageCoalescer.TryCoalesce(
                packages,
                new OutboundPackage(newer, PackageAudience.Global)
            )
        );
        Assert.Single(packages);
        Assert.Same(older, packages[0].Package);
    }

    [Fact]
    public void PickableSnapshotChunksAreNotCoalesced()
    {
        var packages = new List<OutboundPackage>();
        var older = new PickablePositionsPackage
            { Positions = [new PickablePosition(1, new Vector3(1f, 2f, 3f))] };

        var newer = new PickablePositionsPackage
            { Positions = [new PickablePosition(2, new Vector3(4f, 5f, 6f))] };

        packages.Add(new OutboundPackage(older, PackageAudience.Global));
        Assert.False(
            SnapshotPackageCoalescer.TryCoalesce(
                packages,
                new OutboundPackage(newer, PackageAudience.Global)
            )
        );

        var package = Assert.IsType<PickablePositionsPackage>(Assert.Single(packages).Package);
        Assert.Equal((ushort)1, Assert.Single(package.Positions).Id);
    }

    [Fact]
    public void SnapshotsForDifferentAudiencesAreNotCoalesced()
    {
        var firstClient = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var secondClient = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        var packages = new List<OutboundPackage>
        {
            new(
                new OnlineCharacterStatePackage(),
                PackageAudience.To(firstClient)
            )
        };

        Assert.False(
            SnapshotPackageCoalescer.TryCoalesce(
                packages,
                new OutboundPackage(
                    new OnlineCharacterStatePackage(),
                    PackageAudience.To(secondClient)
                )
            )
        );
        Assert.Single(packages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotsDoNotMergeAcrossReusedConnectionIds(bool exclusion)
    {
        var oldClient = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var newClient = new Client(null, 1, Guid.NewGuid(), oldClient.CharacterId, null);
        var packages = new List<OutboundPackage>
        {
            new(
                new OnlineCharacterStatePackage(),
                exclusion ? PackageAudience.Except(oldClient) : PackageAudience.To(oldClient)
            )
        };

        Assert.False(
            SnapshotPackageCoalescer.TryCoalesce(
                packages,
                new OutboundPackage(
                    new OnlineCharacterStatePackage(),
                    exclusion ? PackageAudience.Except(newClient) : PackageAudience.To(newClient)
                )
            )
        );
    }

    [Fact]
    public void EquivalentRecipientSetsCoalesceRegardlessOfOrderOrDuplicates()
    {
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var second = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        var older = new OnlineCharacterStatePackage();
        var newer = new OnlineCharacterStatePackage();
        var packages = new List<OutboundPackage> { new(older, PackageAudience.To([first, second])) };

        Assert.True(
            SnapshotPackageCoalescer.TryCoalesce(
                packages,
                new OutboundPackage(newer, PackageAudience.To([second, first, first]))
            )
        );
        Assert.Same(newer, Assert.Single(packages).Package);
    }
}
