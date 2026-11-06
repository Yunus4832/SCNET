using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;

using Survivalcraft.Test.Modding;

using Random = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class ExplosivesReplicationTest
{
    [Fact]
    public void ActiveFuseFactsRoundTripWithoutAuthoritativeTimerData()
    {
        var point = new Point3(-17, 65, 33);
        Assert.Equal(
            point,
            Assert.IsType<BlockFuseAddedPackage>(
                ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(new BlockFuseAddedPackage { Point = point }))
            ).Point
        );
        Assert.Equal(
            point,
            Assert.IsType<BlockFuseRemovedPackage>(
                ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(new BlockFuseRemovedPackage { Point = point }))
            ).Point
        );
    }

    [Fact]
    public void LiveObserverAndLateChunkBaselineConvergeAndResetReentryAreIdempotent()
    {
        using var pair = new FuseWorlds();
        var point = new Point3(1, 65, 1);
        pair.PlaceKeg(point);
        ((SpatialInterestIndex)Field(pair.Interest, "_spatial")!).SetLocation(pair.Player, new Vector2(1, 1), 32f);
        Assert.True(pair.AuthorityFuses.IgniteFuse(point.X, point.Y, point.Z));
        pair.Deliver();
        Assert.Equal(point, Assert.Single(pair.ReplicaFuses.ActiveFusePoints));
        var replicaNode = pair.ReplicaNode;
        var context = new PackageReceiveContext(replicaNode, replicaNode.Runtime, pair.Server);
        var reset = new ChunkStateResetPackage(new Point2(0, 0));
        new ChunkStateResetPackageHandler().Handle(reset, context);
        new ChunkStateResetPackageHandler().Handle(reset, context);
        Assert.Empty(pair.ReplicaFuses.ActiveFusePoints);
        // Exercise the actual subscribed chunk-entry publisher after interest reset/reentry.
        ((Action<Client, Point2>)Field(pair.Interest, "ChunkEntered")!)(pair.Player, new Point2(0, 0));
        pair.Deliver();
        Assert.Equal(point, Assert.Single(pair.ReplicaFuses.ActiveFusePoints));
        new ComponentOnFirePackageHandler().Handle(new BlockFuseAddedPackage { Point = point }, context);
        Assert.Single(pair.ReplicaFuses.ActiveFusePoints);
        var removals = 0;
        pair.ReplicaFuses.FuseRemoved += _ => removals++;
        pair.AuthorityFuses.OnBlockRemoved(0, 0, point.X, point.Y, point.Z);
        pair.Deliver();
        new ComponentOnFirePackageHandler().Handle(new BlockFuseRemovedPackage { Point = point }, context);
        Assert.Empty(pair.ReplicaFuses.ActiveFusePoints);
        Assert.Equal(1, removals);
    }

    [Fact]
    public void ClientFactsCannotStartOrCancelAuthorityTimers()
    {
        using var pair = new FuseWorlds();
        var handler = new ComponentOnFirePackageHandler();
        var context = new PackageReceiveContext(pair.AuthorityNode, pair.AuthorityNode.Runtime, pair.Player);
        handler.Handle(new BlockFuseAddedPackage { Point = new Point3(1, 65, 1) }, context);
        Assert.Empty(pair.AuthorityFuses.ActiveFusePoints);
        pair.PlaceKeg(new Point3(1, 65, 1));
        pair.AuthorityFuses.IgniteFuse(1, 65, 1);
        handler.Handle(new BlockFuseRemovedPackage { Point = new Point3(1, 65, 1) }, context);
        Assert.Single(pair.AuthorityFuses.ActiveFusePoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientIgnitionOnlyRecognizesTheTargetAndNeverConsumesRandomOrOwnsTimers(bool detonator)
    {
        using var pair = new FuseWorlds();
        var contents = detonator
            ? BuiltInBlockRegistry.GetIndex<DetonatorBlock>()
            : BuiltInBlockRegistry.GetIndex<SmallGunpowderKegBlock>();
        pair.ReplicaTerrain.Terrain.SetCellValueFast(1, 65, 1, Terrain.MakeBlockValue(contents));
        var random = (Random)Field(pair.ReplicaFuses, "_random")!;
        random.Seed(1234);
        var state = random.State;
        Assert.True(pair.ReplicaFuses.IgniteFuse(1, 65, 1));
        Assert.Equal(state, random.State);
        Assert.Empty(pair.ReplicaFuses.ActiveFusePoints);
        pair.ReplicaFuses.ApplyReplicaFuseAdded(new Point3(1, 65, 1));
        pair.ReplicaFuses.Update(100f);
        Assert.Single(pair.ReplicaFuses.ActiveFusePoints);
        var saved = new ValuesDictionary();
        pair.ReplicaFuses.Save(saved);
        Assert.False(saved.ContainsKey("Explosives"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityUsesOriginalFuseDelayAndPersistsTheRemainingCountdown(bool detonator)
    {
        using var pair = new FuseWorlds();
        var contents = detonator
            ? BuiltInBlockRegistry.GetIndex<DetonatorBlock>()
            : BuiltInBlockRegistry.GetIndex<SmallGunpowderKegBlock>();
        pair.AuthorityTerrain.Terrain.SetCellValueFast(1, 65, 1, Terrain.MakeBlockValue(contents));
        ((Random)Field(pair.AuthorityFuses, "_random")!).Seed(1234);
        var expected = new Random(1234).Float(detonator ? .8f : 6f, detonator ? 1.2f : 7f);
        Assert.True(pair.AuthorityFuses.IgniteFuse(1, 65, 1));
        pair.AuthorityFuses.Update(.25f);
        var saved = new ValuesDictionary();
        pair.AuthorityFuses.Save(saved);
        var data = Assert.Single(saved.GetValue<ValuesDictionary>("Explosives").Values.OfType<ValuesDictionary>());
        Assert.Equal(expected - .25f, data.GetValue<float>("TimeToExplosion"));
        Assert.Equal(new Point3(1, 65, 1), data.GetValue<Point3>("Point"));
    }

    [Fact]
    public void FuseBaselineBeforeTerrainInstallationIsRetainedForPresentationRetry()
    {
        using var pair = new FuseWorlds();
        var point = new Point3(1, 65, 1);
        pair.ReplicaFuses.ApplyReplicaFuseAdded(point);
        var owner = new SubsystemExplosivesPresentation();
        pair.Replica.InitializeSubsystem(owner);
        SetField(owner, "_explosives", pair.ReplicaFuses);
        SetField(owner, "_terrain", pair.ReplicaTerrain);
        owner.ReconcileFuseParticles();
        Assert.Equal(point, Assert.Single(pair.ReplicaFuses.ActiveFusePoints));
        Assert.Empty((System.Collections.IDictionary)Field(owner, "_fuses")!);
        owner.Dispose();
    }

    [Fact]
    public void DisposingTheOwnerReleasesEveryRegisteredFuseParticle()
    {
        using var pair = new FuseWorlds();
        var point = new Point3(1, 65, 1);
        pair.ReplicaFuses.ApplyReplicaFuseAdded(point);
        var particles = new SubsystemParticles();
        pair.Replica.InitializeSubsystem(particles);
        // Resource-free fixture: exercise the real particle owner's registration/removal lifetime.
        var fuse = (FuseParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(FuseParticleSystem));
        particles.AddParticleSystem(fuse);
        var owner = new SubsystemExplosivesPresentation();
        pair.Replica.InitializeSubsystem(owner);
        SetField(owner, "_explosives", pair.ReplicaFuses);
        SetField(owner, "_terrain", pair.ReplicaTerrain);
        SetField(owner, "_particles", particles);
        ((Dictionary<Point3, FuseParticleSystem>)Field(owner, "_fuses")!).Add(point, fuse);
        owner.Dispose();
        owner.Dispose();
        Assert.Null(fuse.SubsystemParticles);
        Assert.Empty((System.Collections.IDictionary)Field(owner, "_fuses")!);
        Assert.Equal(point, Assert.Single(pair.ReplicaFuses.ActiveFusePoints));
    }

    private static void SetField(object instance, string name, object value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    }

    private static object? Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }

    private sealed class FuseWorlds : IDisposable
    {
        private readonly WorldCatalogScope _authority;

        private readonly WorldCatalogScope _replica;

        private readonly IDisposable _toClient;

        private readonly IDisposable _toAuthority;

        internal NetNode AuthorityNode { get; } = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );

        internal NetNode ReplicaNode { get; } = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );

        internal Fixtures.RuntimeTestProject Authority => _authority.Project;

        internal Fixtures.RuntimeTestProject Replica => _replica.Project;

        internal SubsystemTerrain AuthorityTerrain { get; } = new() { Terrain = new Terrain() };

        internal SubsystemTerrain ReplicaTerrain { get; } = new() { Terrain = new Terrain() };

        internal SubsystemExplosivesBlockBehavior AuthorityFuses { get; } = new();

        internal SubsystemExplosivesBlockBehavior ReplicaFuses { get; } = new();

        internal SubsystemNetworkInterest Interest { get; } = new();

        internal Client Player { get; }

        internal Client Server { get; }

        internal FuseWorlds()
        {
            _authority = new WorldCatalogScope(AuthorityNode);
            _replica = new WorldCatalogScope(ReplicaNode);
            Setup(Authority, AuthorityTerrain, AuthorityFuses, Interest);
            Setup(Replica, ReplicaTerrain, ReplicaFuses, new SubsystemNetworkInterest());
            Replica.AddSubsystem(new SubsystemSoundGeneratorPresentation());
            Player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
                { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
            Server = new Client(Guid.Empty)
                { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
            AuthorityNode.AddClient(Player);
            ReplicaNode.Server = Server;
            var connections = InProcessMessageConnection.CreatePair(AuthorityNode, Player, ReplicaNode, Server);
            _toClient = connections.ToClient;
            _toAuthority = connections.ToAuthority;
        }

        private static void Setup(
            Fixtures.RuntimeTestProject project,
            SubsystemTerrain terrain,
            SubsystemExplosivesBlockBehavior explosives,
            SubsystemNetworkInterest interest
        )
        {
            terrain.Terrain.AllocateChunk(0, 0);
            project.AddSubsystem(terrain);
            project.AddSubsystem(interest);
            project.AddSubsystem(new SubsystemFireBlockBehavior());
            project.AddSubsystem(new SubsystemExplosions());
            project.AddSubsystem(explosives);
            var empty = new ValuesDictionary();
            empty.SetValue("Explosives", new ValuesDictionary());
            explosives.Load(empty);
            project.AddSubsystem(new SubsystemSignBlockBehavior());
            project.AddSubsystem(new SubsystemItemsScanner());
            var definitions = new ValuesDictionary();
            definitions.SetValue("Blocks", new ValuesDictionary());
            definitions.SetValue("Items", new ValuesDictionary());
            var memory = new SubsystemMemoryBankBlockBehavior();
            var truth = new SubsystemTruthTableCircuitBlockBehavior();
            project.AddSubsystem(memory);
            project.AddSubsystem(truth);
            memory.Load(definitions);
            truth.Load(definitions);
        }

        internal void PlaceKeg(Point3 point)
        {
            AuthorityTerrain.Terrain.SetCellValueFast(
                point.X,
                point.Y,
                point.Z,
                Terrain.MakeBlockValue(BuiltInBlockRegistry.GetIndex<SmallGunpowderKegBlock>())
            );
        }

        internal void Deliver()
        {
            typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(AuthorityNode, null);
            WorldNetworkBinding.For(Replica).Inbox.Drain(
                100,
                received =>
                    new ComponentOnFirePackageHandler().Handle(
                        Assert.IsAssignableFrom<ComponentOnFirePackage>(received.Package),
                        received.Context
                    )
            );
        }

        public void Dispose()
        {
            _toClient.Dispose();
            _toAuthority.Dispose();
            _replica.Dispose();
            _authority.Dispose();
        }
    }
}
