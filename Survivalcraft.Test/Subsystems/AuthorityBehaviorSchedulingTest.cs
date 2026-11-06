using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.Components.Behavior;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class AuthorityBehaviorSchedulingTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, 1)]
    [InlineData(RuntimeSide.Client, 0)]
    public void OnlyAuthorityAdvancesAutonomousBehavior(RuntimeSide side, int expectedUpdates)
    {
        using var project = new RuntimeTestProject(side);
        var update = new SubsystemUpdate { UpdatesPerFrame = 1 };
        var time = new SubsystemTime();
        project.InitializeSubsystem(update);
        project.AddSubsystem(time);
        project.AddSubsystem(new SubsystemCharacters());
        update.Load(new ValuesDictionary());
        var behavior = new BehaviorProbe();
        project.InitializeComponent(behavior);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(behavior.Entity)!).Add(behavior);
        update.OnEntityAdded(behavior.Entity);

        update.Update();

        Assert.Equal(expectedUpdates, behavior.Updates);
        var runtime = WorldRuntimeContext.For(project);
        Assert.Equal(
            side == RuntimeSide.Authority,
            SubsystemUpdate.ShouldScheduleUpdateable(new ComponentPilot(), runtime)
        );
        Assert.Equal(
            side == RuntimeSide.Authority,
            SubsystemUpdate.ShouldScheduleUpdateable(new ComponentPathfinding(), runtime)
        );
        Assert.Equal(
            side == RuntimeSide.Authority,
            SubsystemUpdate.ShouldScheduleUpdateable(new ComponentBehaviorSelector(), runtime)
        );
    }

    [Fact]
    public void DirectClientAiUpdatesDoNotReadSimulationDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var components = new Component[]
            { new ComponentPilot(), new ComponentPathfinding(), new ComponentBehaviorSelector() };
        foreach (var component in components)
        {
            project.InitializeComponent(component);
            ((IUpdateable)component).Update(.1f);
        }
    }

    [Theory]
    [InlineData(RuntimeSide.Client, false, 0, false)]
    [InlineData(RuntimeSide.Client, true, 0, true)]
    [InlineData(RuntimeSide.Authority, false, 1, false)]
    public void ClientSteedPredictsOnlyLocalRidingWithoutAdvancingItsAi(
        RuntimeSide side,
        bool localRider,
        int expectedAiUpdates,
        bool expectedMotion
    )
    {
        using var project = new RuntimeTestProject(side);
        var body = new ComponentBody();
        var locomotion = new ComponentLocomotion();
        var creature = new ComponentCreature { ComponentBody = body, ComponentLocomotion = locomotion };
        var steed = new SteedProbe { IsActive = true, SpeedOrder = 1, TurnOrder = .4f };
        var time = new SubsystemTime();
        project.InitializeComponent(steed);
        project.InitializeComponent(creature);
        project.InitializeComponent(body);
        project.InitializeComponent(locomotion);
        typeof(SubsystemTime).GetProperty(nameof(SubsystemTime.GameTimeDelta))!.SetValue(time, .1f);
        Set(steed, "_componentCreature", creature, typeof(ComponentSteedBehavior));
        Set(steed, "_subsystemTime", time, typeof(ComponentSteedBehavior));
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, localRider);
        var character = new ComponentCharacter { CharacterData = data };
        project.InitializeComponent(character);
        var riderBody = new ComponentBody { Character = character, ParentBody = body };
        project.InitializeComponent(riderBody);

        steed.Update(.1f);

        Assert.Equal(expectedAiUpdates, steed.AiUpdates);
        Assert.Equal(expectedMotion, locomotion.WalkOrder is { Y: > 0f });
        Assert.Equal(0, steed.SpeedOrder);
        Assert.Equal(0f, steed.TurnOrder);
        if (expectedMotion)
        {
            riderBody.ParentBody = null;
            steed.Update(.1f);
            riderBody.ParentBody = body;
            steed.Update(.1f);
            Assert.Equal(0f, locomotion.WalkOrder!.Value.Y);
            Assert.Equal(0, steed.AiUpdates);
        }
    }

    private static void Set(object target, string name, object value, Type? owner = null)
    {
        (owner ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            target,
            value
        );
    }

    private sealed class BehaviorProbe : ComponentBehavior, IUpdateable
    {
        public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

        public int Updates { get; private set; }

        public override float ImportanceLevel => 1f;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt) => Updates++;
    }

    private sealed class SteedProbe : ComponentSteedBehavior
    {
        public int AiUpdates { get; private set; }

        public SteedProbe()
        {
            stateMachine.AddState("Probe", () => { }, () => AiUpdates++, () => { });
            stateMachine.TransitionTo("Probe");
        }
    }
}
