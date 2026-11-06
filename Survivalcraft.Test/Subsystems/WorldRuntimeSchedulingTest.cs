using System.Reflection;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Cameras;
using Game.Rendering;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldRuntimeSchedulingTest
{
    [Theory]
    [InlineData(UpdateExecutionSide.Authority, RuntimeSide.Authority, true)]
    [InlineData(UpdateExecutionSide.Authority, RuntimeSide.Client, false)]
    [InlineData(UpdateExecutionSide.Client, RuntimeSide.Authority, false)]
    [InlineData(UpdateExecutionSide.Client, RuntimeSide.Client, true)]
    [InlineData(UpdateExecutionSide.Both, RuntimeSide.Authority, true)]
    [InlineData(UpdateExecutionSide.Both, RuntimeSide.Client, true)]
    [InlineData(UpdateExecutionSide.Disabled, RuntimeSide.Authority, false)]
    public void ExplicitUpdateSideSelectsTheOwningWorld(
        UpdateExecutionSide side,
        RuntimeSide runtimeSide,
        bool expected
    )
    {
        using var project = new RuntimeTestProject(runtimeSide);
        var runtime = WorldRuntimeContext.For(project);
        var probe = new SideProbe(side);
        Assert.Equal(expected, SubsystemUpdate.ShouldScheduleUpdateable(probe, runtime));
        runtime.Close();
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(probe, runtime));
    }

    [Fact]
    public void InvalidUpdateSideFailsRegistration()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        Assert.Throws<InvalidOperationException>(() =>
            SubsystemUpdate.ShouldScheduleUpdateable(
                new SideProbe((UpdateExecutionSide)99),
                WorldRuntimeContext.For(project)
            )
        );
    }

    private sealed class SideProbe(UpdateExecutionSide side) : IUpdateable
    {
        public UpdateExecutionSide UpdateSide => side;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt)
        {
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingTheWorldDuringAnUpdateSkipsTheRemainingComponentsAndFutureFrames(bool closeDuringClock)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var update = new SubsystemUpdate { UpdatesPerFrame = 1 };
        var time = new SubsystemTime();
        project.InitializeSubsystem(update);
        project.AddSubsystem(time);
        project.AddSubsystem(new SubsystemCharacters());
        update.Load(new ValuesDictionary());
        var pending = (Dictionary<IUpdateable, bool>)GetField(update, "_toAddOrRemove");
        var remainingCalls = 0;
        if (closeDuringClock)
        {
            time.QueueGameTimeDelayedExecution(0, project.Dispose);
        }
        else
        {
            pending.Add(new UpdateProbe(UpdateOrder.Input, project.Dispose), true);
        }

        pending.Add(new UpdateProbe(UpdateOrder.Body, () => remainingCalls++), true);

        update.Update();
        update.Update();

        Assert.True(project.IsDisposed);
        Assert.Equal(0, remainingCalls);
        Assert.False(update.IsLastUpdateInFrame);
    }

    [Fact]
    public void ClosingTheWorldDuringDrawingSkipsRemainingDrawablesAndFutureDraws()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var drawing = new SubsystemDrawing();
        project.InitializeSubsystem(drawing);
        var drawables = (Dictionary<IDrawable, bool>)GetField(drawing, "_drawables");
        var remainingCalls = 0;
        drawables.Add(new DrawProbe(0, project.Dispose), true);
        drawables.Add(new DrawProbe(1, () => remainingCalls++), true);

        drawing.Draw(null!);
        drawing.Draw(null!);

        Assert.True(project.IsDisposed);
        Assert.Equal(0, remainingCalls);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, 0)]
    [InlineData(RuntimeSide.Client, 1)]
    public void OnlyPresentationWorldsRegisterAndDrawSubsystems(RuntimeSide side, int expected)
    {
        using var project = new RuntimeTestProject(side);
        var drawing = new SubsystemDrawing();
        var probe = new DrawableSubsystem();
        project.InitializeSubsystem(drawing);
        project.AddSubsystem(probe);

        drawing.Load(new ValuesDictionary());
        drawing.Draw(null!);

        Assert.Equal(expected, drawing.DrawablesCount);
        Assert.Equal(expected, probe.OrderReads);
        Assert.Equal(expected, probe.DrawCalls);
    }

    [Fact]
    public void ClosingTheRuntimeDuringDrawingStopsBeforeTheProjectIsDisposed()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var runtime = WorldRuntimeContext.For(project);
        var drawing = new SubsystemDrawing();
        project.InitializeSubsystem(drawing);
        var drawables = (Dictionary<IDrawable, bool>)GetField(drawing, "_drawables");
        var remainingCalls = 0;
        drawables.Add(new DrawProbe(0, runtime.Close), true);
        drawables.Add(new DrawProbe(1, () => remainingCalls++), true);

        drawing.Draw(null!);
        drawing.Draw(null!);

        Assert.True(runtime.IsClosed);
        Assert.False(project.IsDisposed);
        Assert.Equal(0, remainingCalls);
    }

    private sealed class DrawableSubsystem : Subsystem, IDrawable
    {
        public int OrderReads { get; private set; }

        public int DrawCalls { get; private set; }

        public int[] DrawOrders
        {
            get
            {
                OrderReads++;
                return [0];
            }
        }

        public void Draw(Camera camera, int drawOrder) => DrawCalls++;
    }

    private static object GetField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    private sealed class UpdateProbe(UpdateOrder order, Action update) : IUpdateable
    {
        public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

        public UpdateOrder UpdateOrder => order;

        public void Update(float dt)
        {
            update();
        }
    }

    private sealed class DrawProbe(int order, Action draw) : IDrawable
    {
        public int[] DrawOrders => [order];

        public void Draw(Camera camera, int drawOrder)
        {
            draw();
        }
    }
}
