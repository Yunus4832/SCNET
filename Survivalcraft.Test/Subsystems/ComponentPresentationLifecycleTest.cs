using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class ComponentPresentationLifecycleTest
{
    [Fact]
    public void AuthorityDoesNotLoadOrUpdatePresentationDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var fire = new ComponentOnFirePresentation();
        var shapeshift = new ComponentShapeshifterPresentation();
        var health = new ComponentHealthPresentation();
        var damage = new ComponentDamagePresentation();
        var sickness = new ComponentSicknessPresentation();
        var flu = new ComponentFluPresentation();
        var sleep = new ComponentSleepPresentation();
        var vitals = new ComponentVitalStatsPresentation();
        project.InitializeComponent(fire);
        project.InitializeComponent(shapeshift);
        project.InitializeComponent(health);
        project.InitializeComponent(damage);
        project.InitializeComponent(sickness);
        project.InitializeComponent(flu);
        project.InitializeComponent(sleep);
        project.InitializeComponent(vitals);
        fire.Entity.IsAddedToProject = true;
        shapeshift.Entity.IsAddedToProject = true;
        health.Entity.IsAddedToProject = true;
        fire.Load(new ValuesDictionary(), null!);
        shapeshift.Load(new ValuesDictionary(), null!);
        health.Load(new ValuesDictionary(), null!);
        damage.Load(new ValuesDictionary(), null!);
        sickness.Load(new ValuesDictionary(), null!);
        sickness.Entity.IsAddedToProject = true;
        sickness.Update(1f);
        flu.Load(new ValuesDictionary(), null!);
        sleep.Load(new ValuesDictionary(), null!);
        vitals.Load(new ValuesDictionary(), null!);
        flu.Entity.IsAddedToProject = sleep.Entity.IsAddedToProject = vitals.Entity.IsAddedToProject = true;
        flu.Update(1f);
        sleep.Update(1f);
        vitals.Update(1f);
        fire.Update(1f);
        shapeshift.Update(1f);
        health.Update(1f);
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(fire, WorldRuntimeContext.For(project)));
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(shapeshift, WorldRuntimeContext.For(project)));
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(health, WorldRuntimeContext.For(project)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FireEffectsStopOnEntityRemovalAndDirectDisposal(bool dispose)
    {
        var presentation = new ComponentOnFirePresentation();
        var particles = (OnFireParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(OnFireParticleSystem));
        Set(presentation, "_particleSystem", particles);
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        Assert.True(particles.IsStopped);
        presentation.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShapeshiftEffectsStopOnEntityRemovalAndDirectDisposal(bool dispose)
    {
        var presentation = new ComponentShapeshifterPresentation();
        var particles =
            (ShapeshiftParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(ShapeshiftParticleSystem));
        Set(presentation, "_particleSystem", particles);
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        Assert.True(particles.Stopped);
        presentation.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VomitPresentationStopsOnRemovalAndDirectDisposal(bool dispose)
    {
        var presentation = new ComponentSicknessPresentation();
        var particles = (PukeParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(PukeParticleSystem));
        Set(presentation, "_puke", particles);
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        Assert.True(particles.IsStopped);
        presentation.Dispose();
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
