using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Components;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class MinerPresentationOwnershipTest
{
    private static readonly string[] _eventNames =
    [
        "ToolLevelRequired", "ToolHintRequested", "ItemDisabled", "FriendlyFireRejected", "DigImpact", "BlockDug",
        "BlockPlaced", "MeleeSwing"
    ];

    [Fact]
    public void AuthorityPresentationLoadsWithoutAudioParticlesOrMiner()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var presentation = new ComponentMinerPresentation();
        project.InitializeComponent(presentation);
        presentation.Load(new ValuesDictionary(), null!);
        presentation.OnEntityAdded();
        Assert.Null(
            typeof(ComponentMinerPresentation).GetField("_miner", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(presentation)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedOrDisposedPresentationUnsubscribesEveryFeedbackEvent(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var miner = new ComponentMiner();
        var presentation = new ComponentMinerPresentation();
        project.InitializeComponent(presentation);
        typeof(ComponentMinerPresentation).GetField("_miner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(presentation, miner);
        presentation.OnEntityAdded();
        presentation.OnEntityAdded();
        foreach (var name in _eventNames) { Assert.Single(ReadEvent(miner, name)!.GetInvocationList()); }

        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        foreach (var name in _eventNames) { Assert.Null(ReadEvent(miner, name)); }
    }

    [Fact]
    public void ClosedClientRejectsFeedbackBeforeTouchingAbsentDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var miner = new ComponentMiner();
        var presentation = new ComponentMinerPresentation();
        project.InitializeComponent(presentation);
        typeof(ComponentMinerPresentation).GetField("_miner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(presentation, miner);
        presentation.OnEntityAdded();
        WorldRuntimeContext.For(project).Close();
        ReadEvent(miner, "BlockDug")!.DynamicInvoke(
            new BlockDigOutcome(0, 0, default, Vector3.Zero, BlockDigOutcomeKind.BlockChanged, true)
        );
        ReadEvent(miner, "DigImpact")!.DynamicInvoke(0, Vector3.Zero, 1f);
        ReadEvent(miner, "MeleeSwing")!.DynamicInvoke(Vector3.Zero);
    }

    [Theory]
    [InlineData(RuntimeSide.Client, false)]
    [InlineData(RuntimeSide.Authority, true)]
    public void ClientOrClosedWorldCannotApplyAttackRules(RuntimeSide side, bool close)
    {
        using var project = new RuntimeTestProject(side);
        var body = new ComponentBody();
        project.InitializeComponent(body);
        var locomotion = new ComponentLocomotion();
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(body.Entity)!).Add(locomotion);
        if (close) { WorldRuntimeContext.For(project).Close(); }

        ComponentMiner.AttackBody(body, null, Vector3.Zero, Vector3.UnitX, 10, true);
        Assert.Equal(Vector3.Zero, body.Velocity);
        Assert.Equal(0f, locomotion.StunTime);
    }

    private static Delegate? ReadEvent(ComponentMiner miner, string name)
    {
        return (Delegate?)typeof(ComponentMiner).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(miner);
    }
}
