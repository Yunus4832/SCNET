using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

using Random = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FireworksAuthorityTest
{
    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 14)]
    public void AuthorityOwnsTheDelayRemovalAndExactlyOneExplosion(int altitude, int frames)
    {
        using var world = new Fixture(RuntimeSide.Authority);
        var effects = new List<ProjectileEffect>();
        world.Projectiles.EffectRequested += effects.Add;
        var projectile = new Projectile
        {
            Value = Terrain.MakeBlockValue(215, 0, FireworksBlock.SetAltitude(0, altitude)),
            Position = new Vector3(8, 80, 8)
        };
        world.Fireworks.OnFiredAsProjectile(projectile);
        Assert.Equal(ProjectileTrailKind.Fireworks, projectile.Trail!.Kind);
        Assert.Equal(ProjectilePackage.EffectType.FireworksWhoosh, Assert.Single(effects).Kind);
        for (var i = 0; i < frames; i++) { world.Time.NextFrame(.1f, () => default); }

        Assert.True(projectile.ToRemove);
        Assert.Equal(1, effects.Count(effect => effect.Kind == ProjectilePackage.EffectType.FireworksExplosion));
        world.Time.NextFrame(.1f, () => default);
        Assert.Equal(2, effects.Count);
        Assert.Null(world.Project.FindSubsystem<SubsystemParticles>());
    }

    [Fact]
    public void ActualReplicaBaselineInitializesOnlyTrailAndNeverSchedulesItsOwnExplosion()
    {
        using var world = new Fixture(RuntimeSide.Client);
        var effects = new List<ProjectileEffect>();
        world.Projectiles.EffectRequested += effects.Add;
        var behaviors = new SubsystemBlockBehaviors();
        var table = Enumerable.Range(0, BlockRuntimeCatalog.Capacity).Select(_ => Array.Empty<SubsystemBlockBehavior>())
            .ToArray();
        table[215] = [world.Fireworks];
        Set(behaviors, "_blockBehaviorsByContents", table);
        Set(world.Projectiles, "_subsystemBlockBehaviors", behaviors);
        Set(world.Projectiles, "_subsystemTerrain", world.TerrainSubsystem);
        Set(world.Projectiles, "_subsystemGameInfo", new SubsystemGameInfo());
        var projectile = world.Projectiles.AddReplicatedProjectile(
            new ProjectileAddedPackage
            {
                NetworkId = 42, IsFireProjectile = true, Value = Terrain.MakeBlockValue(215),
                Position = new Vector3(8, 80, 8), Velocity = Vector3.UnitY
            },
            null
        );
        for (var i = 0; i < 20; i++) { world.Time.NextFrame(.1f, () => default); }

        Assert.False(projectile.ToRemove);
        Assert.Equal(ProjectileTrailKind.Fireworks, projectile.Trail!.Kind);
        Assert.Empty(effects);
        Assert.Empty((System.Collections.IEnumerable)Get(world.Time, "_delayedExecutionsRequests")!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThreeRealOcclusionRaysAndPopPitchConsumeTheOriginalRuleRandomSequence(bool blocked)
    {
        using var world = new Fixture(RuntimeSide.Authority);
        if (blocked)
        {
            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++) { world.TerrainSubsystem.Terrain.SetCellValueFast(x, 70, z, 3); }
            }
        }

        var random = (Random)Get(world.Fireworks, "_random")!;
        random.Seed(12345);
        var reference = new Random(12345);
        for (var i = 0; i < (blocked ? 1 : 3); i++)
        {
            _ = reference.Float(-3f, 3f);
            _ = reference.Float(-3f, 3f);
        }

        var expectedPitch = blocked ? 0f : reference.Float(-.4f, 0f);
        var effects = new List<ProjectileEffect>();
        world.Projectiles.EffectRequested += effects.Add;
        world.Fireworks.ExplodeFireworks(new Vector3(8, 80, 8), 0);
        Assert.Equal(reference.State, random.State);
        if (blocked) { Assert.Empty(effects); }
        else { Assert.Equal(expectedPitch, Assert.Single(effects).Pitch); }
    }

    [Fact]
    public void ExplicitAuthorityPitchSurvivesTheEffectCodecAndClientDirectionGate()
    {
        using var client = new Fixture(RuntimeSide.Client);
        using var authority = new Fixture(RuntimeSide.Authority);
        var effects = new List<ProjectileEffect>();
        client.Projectiles.EffectRequested += effects.Add;
        var package = new ProjectileEffectPackage
        {
            Effect = ProjectilePackage.EffectType.FireworksExplosion, Value = 123,
            Position = new Vector3(1, 80, 1), Pitch = -.3f
        };
        var decoded =
            Assert.IsType<ProjectileEffectPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package)));
        var handler = new ProjectilePackageHandler();
        handler.Handle(decoded, new PackageReceiveContext(client.Node, client.Node.Runtime, null));
        handler.Handle(decoded, new PackageReceiveContext(authority.Node, authority.Node.Runtime, null));
        Assert.Equal(
            new ProjectileEffect(package.Effect, package.Value, package.Position, package.Pitch),
            Assert.Single(effects)
        );
    }

    [Fact]
    public void FireworksEffectReachesTheRealInProcessObserverWithTheAuthorityPitch()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var source = WorldNetworkBinding.For(authority).RequireNode();
        var destination = WorldNetworkBinding.For(client).RequireNode();
        var observer = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
            { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
        source.AddClient(observer);
        var server = new Client(Guid.Empty);
        var pair = InProcessMessageConnection.CreatePair(source, observer, destination, server);
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        var interest = new SubsystemNetworkInterest();
        authority.AddSubsystem(interest);
        ((SpatialInterestIndex)Get(interest, "_spatial")!).SetLocation(observer, Vector2.Zero, 32f);
        var sourceRules = new SubsystemProjectiles();
        var clientRules = new SubsystemProjectiles();
        authority.AddSubsystem(sourceRules);
        client.AddSubsystem(clientRules);
        var effects = new List<ProjectileEffect>();
        clientRules.EffectRequested += effects.Add;
        sourceRules.PublishFireworksEffect(
            ProjectilePackage.EffectType.FireworksExplosion,
            123,
            new Vector3(1, 80, 1),
            -.25f
        );
        typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            source,
            null
        );
        Assert.Equal(
            1,
            WorldNetworkBinding.For(client).Inbox.Drain(
                10,
                message =>
                    new ProjectilePackageHandler().Handle(
                        Assert.IsAssignableFrom<ProjectilePackage>(message.Package),
                        message.Context
                    )
            )
        );
        Assert.Equal(-.25f, Assert.Single(effects).Pitch);
    }

    private static object? Get(object owner, string field)
    {
        return owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
    }

    private static void Set(object owner, string field, object value)
    {
        owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WorldCatalogScope _scope;

        internal NetNode Node { get; }

        internal RuntimeTestProject Project => _scope.Project;

        internal SubsystemTerrain TerrainSubsystem { get; } = new() { Terrain = new Terrain() };

        internal SubsystemTime Time { get; } = new();

        internal SubsystemProjectiles Projectiles { get; } = new();

        internal SubsystemFireworksBlockBehavior Fireworks { get; } = new();

        internal Fixture(RuntimeSide side)
        {
            Node = new NetNode(
                    NetNodeOptions.Default,
                    side == RuntimeSide.Client,
                    PackageHandlerCatalog.Empty,
                    side == RuntimeSide.Client ? NodeRole.Client : NodeRole.Authority
                )
                ;
            _scope = new WorldCatalogScope(Node);
            TerrainSubsystem.Terrain.AllocateChunk(0, 0);
            Project.AddSubsystem(TerrainSubsystem);
            Project.AddSubsystem(Time);
            Project.AddSubsystem(Projectiles);
            Project.AddSubsystem(new SubsystemSky());
            Project.AddSubsystem(new SubsystemCharacters());
            Project.AddSubsystem(new SubsystemNetworkInterest());
            Project.AddSubsystem(new SubsystemBodies());
            var noise = new SubsystemNoise();
            Project.AddSubsystem(noise);
            noise.Load(new ValuesDictionary());
            Project.AddSubsystem(Fireworks);
            Fireworks.Load(new ValuesDictionary());
        }

        public void Dispose() => _scope.Dispose();
    }
}
