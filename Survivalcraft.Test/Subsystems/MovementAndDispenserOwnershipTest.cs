using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Components;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class MovementAndDispenserOwnershipTest
{
    [Theory]
    [InlineData(RuntimeSide.Client, false)]
    [InlineData(RuntimeSide.Authority, true)]
    public void ClientOrClosedDispenserCannotConsumeInventoryOrSpawnAnything(RuntimeSide side, bool close)
    {
        using var project = new RuntimeTestProject(side);
        var dispenser = new ComponentDispenser();
        project.InitializeComponent(dispenser);
        if (close) { WorldRuntimeContext.For(project).Close(); }

        dispenser.Dispense();
        dispenser.DispenseItem(Point3.Zero, 0, 1, DispenserBlock.Mode.Shoot);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, false)]
    [InlineData(RuntimeSide.Client, true)]
    public void SoundResultCannotPlayOnAuthorityOrAfterWorldClosure(RuntimeSide side, bool close)
    {
        using var project = new RuntimeTestProject(side);
        var runtime = WorldRuntimeContext.For(project);
        if (close) { runtime.Close(); }

        new DispenserSoundPackageHandler().Handle(
            new DispenserSoundPackage(),
            new PackageReceiveContext(Game.Network.WorldNetworkBinding.For(runtime).RequireNode(), runtime, null)
        );
    }

    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void BoatDensityPredictionDoesNotNeedAudioAndClientSubmersionCannotRunDamage(RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side);
        var boat = CreateBoat(project, out var body, out var damage);
        body.ImmersionFactor = side == RuntimeSide.Client ? 1f : .5f;
        damage.HitPoints = .5f;
        boat.Update(.1f);
        Assert.Equal(.7f, body.Density);
        Assert.Equal(.5f, damage.HitPoints);
    }

    [Fact]
    public void BoatPresentationTracksThresholdsOnceAndDoesNotReplayInitialDamage()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var boat = CreateBoat(project, out var body, out var damage);
        var audio = new SubsystemAudio();
        project.AddSubsystem(audio);
        SetField(audio, "_subsystemTime", new SubsystemTime());
        var view = new ComponentBoatPresentation();
        view.Initialize(boat.Entity, boat.ValuesDictionary);
        body.ImmersionFactor = .5f;
        damage.HitPoints = .2f;
        view.Load(new ValuesDictionary(), null!);
        boat.Entity.IsAddedToProject = true;
        view.OnEntityAdded();
        view.Update(.1f);
        var queue = (IList)ReadField(audio, "_queuedSounds")!;
        Assert.Empty(queue);
        damage.HitPoints = 1;
        view.Update(.1f);
        damage.HitPoints = .5f;
        view.Update(.1f);
        view.Update(.1f);
        Assert.Single(queue);
        damage.HitPoints = .2f;
        view.Update(.1f);
        Assert.Equal(2, queue.Count);
        view.OnEntityRemoved();
        view.OnEntityAdded();
        view.Update(.1f);
        Assert.Equal(2, queue.Count);
        view.Dispose();
    }

    [Fact]
    public void AuthorityPresentationLoadsWithoutPlaybackOrLocomotionDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        foreach (var view in new Component[] { new ComponentBoatPresentation(), new ComponentLocomotionPresentation() })
        {
            project.InitializeComponent(view);
            view.Load(new ValuesDictionary(), null!);
            view.OnEntityAdded();
            view.OnEntityRemoved();
            view.Dispose();
        }
    }

    [Fact]
    public void LocomotionPresentationUnsubscribesBothEventsAndRejectsOldCallbacks()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var locomotion = new ComponentLocomotion();
        var view = new ComponentLocomotionPresentation();
        project.InitializeComponent(view);
        SetField(view, "_locomotion", locomotion);
        view.OnEntityAdded();
        view.OnEntityAdded();
        var step = (Delegate)ReadField(locomotion, "FootstepRequested")!;
        Assert.Single(step.GetInvocationList());
        view.OnEntityRemoved();
        Assert.Null(ReadField(locomotion, "FootstepRequested"));
        Assert.Null(ReadField(locomotion, "FootwearHintRequested"));
        step.DynamicInvoke(1f);
        view.Dispose();
    }

    private static ComponentBoat CreateBoat(
        RuntimeTestProject project,
        out ComponentBody body,
        out ComponentDamage damage
    )
    {
        project.AddSubsystem(new SubsystemTime());
        var boat = new ComponentBoat();
        project.InitializeComponent(boat);
        body = new ComponentBody();
        damage = new ComponentDamage();
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(boat.Entity)!).AddRange([body, damage, new ComponentMount { ComponentBody = body }]);
        boat.Load(new ValuesDictionary(), null!);
        return boat;
    }

    private static object? ReadField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
