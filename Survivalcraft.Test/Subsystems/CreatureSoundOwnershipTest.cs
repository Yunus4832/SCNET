using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Components;
using Game.Components.Behavior;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class CreatureSoundOwnershipTest
{
    [Theory]
    [InlineData("Idle", "_idleSound", "_lastSoundTime")]
    [InlineData("Pain", "_painSound", "_lastSoundTime")]
    [InlineData("Moan", "_moanSound", "_lastSoundTime")]
    [InlineData("Sneeze", "_sneezeSound", "_lastSoundTime")]
    [InlineData("Cough", "_coughSound", "_lastCoughingSoundTime")]
    [InlineData("Puke", "_pukeSound", "_lastPukeSoundTime")]
    [InlineData("Attack", "_attackSound", "_lastSoundTime")]
    public void AuthoritySchedulesSoundsWithoutPlaybackDependencies(string kind, string soundField, string clockField)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemNetworkInterest());
        var sounds = new ComponentCreatureSounds();
        project.InitializeComponent(sounds);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(sounds.Entity)!).Add(new ComponentBody());
        SetField(sounds, "_subsystemTime", new SubsystemTime());
        SetField(sounds, soundField, "Audio/Test");
        var arguments = kind == "Idle" ? new object[] { false } : [];

        // No Audio or Creature component: authority publication must only need routing and cooldown state.
        typeof(ComponentCreatureSounds).GetMethod($"Play{kind}Sound")!.Invoke(sounds, arguments);
        Assert.Equal(0d, (double)GetField(sounds, clockField));
        var presentation = new ComponentCreatureSoundsPresentation();
        project.InitializeComponent(presentation);
        presentation.Load(new EntitySystem.TemplatesDatabase.ValuesDictionary(), null!);
        Assert.False(
            (bool)typeof(ComponentCreatureSoundsPresentation).GetMethod($"Play{kind}Sound")!
                .Invoke(presentation, arguments)!
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedOrDisposedPresentationCannotReachPlaybackDependencies(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var presentation = new ComponentCreatureSoundsPresentation();
        project.InitializeComponent(presentation);
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        Assert.False(presentation.PlayIdleSound(false));
        Assert.False(presentation.PlayPainSound());
        Assert.False(presentation.PlayFootstepSound(1));
        presentation.PlayHowlSound();
        presentation.PlayWingsSound();
        presentation.PlayEggLaidSound();
    }

    [Fact]
    public void ClosedWorldRejectsAuthorityAndPresentationCallsWithoutAdvancingCooldowns()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var sounds = new ComponentCreatureSounds();
        project.InitializeComponent(sounds);
        SetField(sounds, "_idleSound", "Audio/Test");
        WorldRuntimeContext.For(project).Close();
        sounds.PlayIdleSound(false);
        Assert.Equal(-1000d, (double)GetField(sounds, "_lastSoundTime"));

        using var clientProject = new RuntimeTestProject(RuntimeSide.Client);
        var presentation = new ComponentCreatureSoundsPresentation();
        clientProject.InitializeComponent(presentation);
        WorldRuntimeContext.For(clientProject).Close();
        Assert.False(presentation.PlayIdleSound(false));
        Assert.False(presentation.PlayFootstepSound(1));
        presentation.PlayHowlSound();
        presentation.PlayWingsSound();
        presentation.PlayEggLaidSound();
        Assert.Equal(-1000d, (double)GetField(presentation, "_lastSoundTime"));
    }

    [Fact]
    public void AuthorityFeedingSoundUsesOnlyEventAndCooldownState()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemNetworkInterest());
        var sounds = new ComponentCreatureSounds();
        project.InitializeComponent(sounds);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(sounds.Entity)!).Add(new ComponentBody());
        SetField(sounds, "_subsystemTime", new SubsystemTime());
        sounds.PlayFeedingSound();
        Assert.Equal(0d, (double)GetField(sounds, "_lastFeedingSoundTime"));
        sounds.PlayFeedingSound();
        Assert.Equal(0d, (double)GetField(sounds, "_lastFeedingSoundTime"));
    }

    [Theory]
    [InlineData("Howl")]
    [InlineData("Wings")]
    [InlineData("EggLaid")]
    public void AuthorityBehaviorSoundPublicationDoesNotRequirePlaybackDependencies(string kind)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemNetworkInterest());
        var sounds = new ComponentCreatureSounds();
        project.InitializeComponent(sounds);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(sounds.Entity)!).Add(new ComponentBody());
        typeof(ComponentCreatureSounds).GetMethod($"Play{kind}Sound")!.Invoke(sounds, null);
    }

    [Theory]
    [InlineData(typeof(ComponentHowlBehavior))]
    [InlineData(typeof(ComponentFlyAwayBehavior))]
    [InlineData(typeof(ComponentLayEggBehavior))]
    public void ClientCannotRunAuthorityBehaviorBeforeRuleDependenciesAreLoaded(Type type)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var behavior = (Component)Activator.CreateInstance(type)!;
        project.InitializeComponent(behavior);
        ((IUpdateable)behavior).Update(1f);
        if (behavior is ComponentFlyAwayBehavior flyAway) { flyAway.HearNoise(null, Vector3.Zero, 1f); }
    }

    private static object GetField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
