using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class UpdateLifetimeTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void ClosedWorldDoesNotAcceptNewGameTimeCallbacks(RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side);
        var time = new SubsystemTime();
        project.InitializeSubsystem(time);
        WorldRuntimeContext.For(project).Close();

        time.QueueGameTimeDelayedExecution(0, () => throw new InvalidOperationException("Closed callback"));
        time.NextFrame(0f, () => throw new InvalidOperationException("Closed population query"));

        var requests = (System.Collections.ICollection)typeof(SubsystemTime)
            .GetField(
                "_delayedExecutionsRequests",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .GetValue(time)!;
        Assert.Empty(requests);
        Assert.Equal(0, time.GameTime);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, ClosePhase.RealTime)]
    [InlineData(RuntimeSide.Client, ClosePhase.RealTime)]
    [InlineData(RuntimeSide.Authority, ClosePhase.GameTime)]
    [InlineData(RuntimeSide.Client, ClosePhase.GameTime)]
    [InlineData(RuntimeSide.Authority, ClosePhase.Hook)]
    [InlineData(RuntimeSide.Client, ClosePhase.Hook)]
    [InlineData(RuntimeSide.Authority, ClosePhase.Component)]
    [InlineData(RuntimeSide.Client, ClosePhase.Component)]
    public void ClosingDuringAnUpdateStopsTheRemainingWork(RuntimeSide side, ClosePhase phase)
    {
        using var project = new RuntimeTestProject(side, bindRuleHooks: false);
        var runtime = WorldRuntimeContext.For(project);
        var update = new SubsystemUpdate();
        var time = new SubsystemTime { FixedTimeStep = .05f };
        var later = new LaterProbe();
        project.AddSubsystem(new SubsystemCharacters());
        project.AddSubsystem(time);
        project.AddSubsystem(update);
        project.AddSubsystem(later);
        if (phase == ClosePhase.Component) { project.AddSubsystem(new ClosingProbe(runtime)); }

        time.Load(new ValuesDictionary());
        update.Load(new ValuesDictionary());
        var delayedCalls = 0;
        if (phase == ClosePhase.RealTime) { runtime.QueueTimeDelayedExecution(0, runtime.Close); }

        if (phase == ClosePhase.GameTime)
        {
            time.QueueGameTimeDelayedExecution(0, runtime.Close);
            time.QueueGameTimeDelayedExecution(0, () => delayedCalls++);
        }

        using var mods = GameModRuntime.Start(
            [
                new ModDescriptor(
                    new ModManifest("test.update-close", "Update close", "1.0.0"),
                    () => new CloseMod(phase == ClosePhase.Hook ? runtime.Close : () => { })
                )
            ]
        );
        project.BindRuleHooks(mods.Gameplay, mods.BlockBehaviors);

        {
            update.Update();
            var stoppedAt = time.GameTime;
            update.Update();

            Assert.True(runtime.IsClosed);
            Assert.Equal(0, later.Calls);
            Assert.Equal(0, delayedCalls);
            Assert.Equal(stoppedAt, time.GameTime);
            Assert.False(update.IsLastUpdateInFrame);
            if (phase == ClosePhase.RealTime) { Assert.Equal(0, stoppedAt); }
        }
    }

    public enum ClosePhase
    {
        RealTime = 0,

        GameTime = 1,

        Hook = 2,

        Component = 3
    }

    private sealed class ClosingProbe(WorldRuntimeContext runtime) : Subsystem, IUpdateable
    {
        public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

        public UpdateOrder UpdateOrder => 0;

        public void Update(float dt) => runtime.Close();
    }

    private sealed class LaterProbe : Subsystem, IUpdateable
    {
        public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

        public int Calls { get; private set; }

        public UpdateOrder UpdateOrder => (UpdateOrder)1;

        public void Update(float dt) => Calls++;
    }

    private sealed class CloseMod(Action close) : IMod
    {
        public void Configure(IModContext context) => context.Gameplay.OnWorldUpdating(_ => close());

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }
}
