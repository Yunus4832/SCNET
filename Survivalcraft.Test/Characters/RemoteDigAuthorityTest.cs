using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Characters;

[Collection(ConfigFileCollection.Name)]
public sealed class RemoteDigAuthorityTest
{
    [Fact]
    public void AuthorityRecastsActualTerrainAndReturnsItsHitRatherThanClaimedDistanceOrValue()
    {
        using var fixture = new DigFixture();
        var claimed = fixture.Hit();
        claimed.Distance = 999f;
        claimed.Value = 12345;
        var (accepted, actual) = fixture.Resolve(claimed);
        Assert.True(accepted);
        Assert.Equal(3, actual.Value);
        Assert.NotEqual(claimed.Distance, actual.Distance);
        Assert.Equal(claimed.CellFace, actual.CellFace);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("different-cell")]
    [InlineData("different-face")]
    [InlineData("out-of-reach")]
    [InlineData("occluded")]
    [InlineData("zero-ray")]
    public void RejectedCompletionDoesNotDigAndAcknowledgesTheOriginalPrediction(string rejection)
    {
        using var fixture = new DigFixture();
        var claimed = fixture.Hit();
        var ray = fixture.Ray;
        switch (rejection)
        {
            case "empty": fixture.Chunk.SetCellValueFast(1, 64, 1, 0); break;
            case "different-cell": claimed.CellFace = new CellFace(2, 64, 1, claimed.CellFace.Face); break;
            case "different-face": claimed.CellFace = new CellFace(1, 64, 1, (claimed.CellFace.Face + 1) % 6); break;
            case "out-of-reach": fixture.Rules.CreativeReach = 0.25f; break;
            case "occluded": fixture.Chunk.SetCellValueFast(1, 65, 1, 3); break;
            case "zero-ray": ray.Direction = Vector3.Zero; break;
        }

        fixture.Character.AddDigEvent(SyncAction.Complete, ray, claimed, 42, 3);
        fixture.Process();
        Assert.Equal(rejection == "empty" ? 0 : 3, fixture.Chunk.GetCellValueFast(1, 64, 1));
        var package = Assert.Single(fixture.Pending);
        var acknowledgement = Assert.IsType<TerrainCellEditCompletedPackage>(package.Package);
        Assert.Equal(42, acknowledgement.EditRequestId);
        Assert.Equal(claimed.CellFace.Point, acknowledgement.EditedCell);
        Assert.Same(fixture.Actor, package.Audience.SingleRecipient);
        Assert.Null(fixture.Character.ComponentMiner.DigCellFace);
    }

    [Fact]
    public void RaycastFailureStillCompletesTheOriginalPredictionBeforePropagating()
    {
        using var fixture = new DigFixture();
        var claimed = fixture.Hit();
        typeof(ComponentMiner).GetField("_subsystemBodies", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            fixture.Character.ComponentMiner,
            null
        );
        fixture.Character.AddDigEvent(SyncAction.Complete, fixture.Ray, claimed, 43, 3);
        var failure = Assert.Throws<TargetInvocationException>(fixture.Process);
        Assert.IsType<NullReferenceException>(failure.InnerException);
        var acknowledgement = Assert.IsType<TerrainCellEditCompletedPackage>(Assert.Single(fixture.Pending).Package);
        Assert.Equal(43, acknowledgement.EditRequestId);
        Assert.Equal(claimed.CellFace.Point, acknowledgement.EditedCell);
    }

    [Fact]
    public void ObservingClientDoesNotRequireAuthorityRaycastDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var character = new ComponentCharacter();
        project.InitializeComponent(character);
        var claimed = new TerrainRaycastResult { CellFace = new CellFace(1, 64, 1, 4), Distance = 2f };
        var args = new object[]
        {
            new ComponentCharacter.DigEventItem
                { NetDigRay = new Ray3(Vector3.Zero, Vector3.UnitY), NetDigRaycast = claimed },
            default(TerrainRaycastResult)
        };
        Assert.True((bool)Resolver.Invoke(character, args)!);
        Assert.Equal(claimed.CellFace, ((TerrainRaycastResult)args[1]).CellFace);
    }

    [Fact]
    public void NonFiniteClaimIsRejectedBeforeRaycast()
    {
        using var fixture = new DigFixture();
        var claimed = fixture.Hit();
        claimed.Distance = float.NaN;
        Assert.False(fixture.Resolve(claimed).Accepted);
    }

    private static MethodInfo Resolver => typeof(ComponentCharacter).GetMethod(
        "TryResolveRemoteDigHit",
        BindingFlags.Instance | BindingFlags.NonPublic
    )!;

    private sealed class DigFixture : IDisposable
    {
        private readonly WorldCatalogScope _world;

        private readonly RuntimeTestProject _replica;

        private readonly InProcessMessageConnection _toClient;

        private readonly InProcessMessageConnection _toAuthority;

        private readonly Terrain _terrain = new();

        public DigFixture()
        {
            var node = new NetNode(
                NetNodeOptions.Default,
                true,
                Game.Network.Handling.PackageHandlerCatalog.Empty,
                NodeRole.Authority
            );
            _world = new WorldCatalogScope(node);
            _replica = new RuntimeTestProject(RuntimeSide.Client);
            Actor = new Client(Guid.NewGuid());
            node.AddClient(Actor);
            var pair = InProcessMessageConnection.CreatePair(
                node,
                Actor,
                WorldNetworkBinding.For(_replica).RequireNode(),
                new Client(Guid.Empty)
            );
            _toClient = pair.ToClient;
            _toAuthority = pair.ToAuthority;
            Pending = (PendingLogicalMessages)typeof(NetNode).GetField(
                "_pendingPackages",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(node)!;
            Chunk = _terrain.AllocateChunk(0, 0);
            Chunk.SetCellValueFast(1, 64, 1, 3);
            var subsystem = new SubsystemTerrain { Terrain = _terrain };
            _world.Project.InitializeSubsystem(subsystem);
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!.SetValue(
                subsystem,
                new TerrainCellAuthority(_terrain)
            );
            Character = new ComponentCharacter();
            _world.Project.InitializeComponent(Character);
            Set(Character, "_subsystemTerrain", subsystem);
            var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(data, _world.Project);
            data.CharacterId = Actor.CharacterId;
            Character.CharacterData = data;
            var body = new ComponentBody { Position = Ray.Position, Rotation = Quaternion.Identity };
            Character.ComponentBody = body;
            var pose = new ComponentCreaturePose();
            Set(pose, "_creature", Character);
            Character.ComponentCreaturePose = pose;
            var miner = new ComponentMiner();
            _world.Project.InitializeComponent(miner);
            Character.ComponentMiner = miner;
            typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.ComponentCreature))!.SetValue(miner, Character);
            typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.ComponentCharacter))!.SetValue(miner, Character);
            Set(miner, "_subsystemGameInfo", new SubsystemGameInfo { WorldSettings = Rules });
            Set(miner, "_subsystemBodies", new SubsystemBodies());
            Set(miner, "_subsystemMovingBlocks", new SubsystemMovingBlocks());
            Set(miner, "_subsystemTerrain", subsystem);
        }

        public Ray3 Ray { get; } = new(new Vector3(1.5f, 66f, 1.5f), -Vector3.UnitY);

        public WorldSettings Rules { get; } = new() { GameMode = GameMode.Creative, CreativeReach = 7.5f };

        public TerrainChunk Chunk { get; }

        public ComponentCharacter Character { get; }

        public Client Actor { get; }

        public PendingLogicalMessages Pending { get; }

        public TerrainRaycastResult Hit() =>
            Character.ComponentMiner.Raycast<TerrainRaycastResult>(Ray, RaycastMode.Digging)!.Value;

        public (bool Accepted, TerrainRaycastResult Hit) Resolve(TerrainRaycastResult claimed)
        {
            var args = new object[]
            {
                new ComponentCharacter.DigEventItem { NetDigRay = Ray, NetDigRaycast = claimed },
                default(TerrainRaycastResult)
            };
            var accepted = (bool)Resolver.Invoke(Character, args)!;
            return (accepted, (TerrainRaycastResult)args[1]);
        }

        public void Process() =>
            typeof(ComponentCharacter).GetMethod("UpdateRemoteDig", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Character, null);

        public void Dispose()
        {
            _toClient.Dispose();
            _toAuthority.Dispose();
            _replica.Dispose();
            _terrain.Dispose();
            _world.Dispose();
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
