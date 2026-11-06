using System.Reflection;

using Game.Characters;
using Game.Components;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class SicknessSimulationOwnershipTest
{
    [Fact]
    public void AuthorityPukingExpiresWithoutParticlesAndRetainsItsCooldown()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var clock = new SubsystemTime();
        project.AddSubsystem(clock);
        var noise = new SubsystemNoise();
        project.AddSubsystem(noise);
        Set(noise, "_subsystemBodies", new SubsystemBodies());
        var sounds = new ComponentCreatureSounds();
        project.InitializeComponent(sounds);
        var character = new TestCharacter
        {
            ComponentBody = new ComponentBody(), ComponentCreatureSounds = sounds,
            ComponentHealth = new ComponentHealth { Health = .05f }
        };
        var sickness = new ComponentSickness();
        project.InitializeComponent(sickness);
        Set(sickness, "_componentCharacter", character);
        Set(sickness, "_subsystemTime", clock);

        // There is deliberately no terrain, particle subsystem, texture cache or audio device.
        sickness.Puke();
        Assert.True(sickness.IsPuking);
        Assert.Equal(1, character.CharacterStats.TimesPuked);

        SetTime(clock, 4);
        Assert.False(sickness.IsPuking);
        sickness.NauseaEffect();
        Assert.False(sickness.IsPuking);
        Assert.Equal(1, character.CharacterStats.TimesPuked);

        SetTime(clock, 51);
        sickness.NauseaEffect();
        Assert.True(sickness.IsPuking);
        Assert.Equal(2, character.CharacterStats.TimesPuked);
    }

    [Fact]
    public void ReplicaAppliesEffectStateWithoutDiseaseRulesAndDuplicatePacketsDoNotRestartEffects()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var clock = new SubsystemTime();
        var sickness = new ComponentSickness();
        project.InitializeComponent(sickness);
        Set(sickness, "_subsystemTime", clock);
        sickness.ApplyState(900, 1, 3.5f, .8f, true);
        sickness.Puke();
        sickness.NauseaEffect();
        sickness.StartSickness();
        Assert.Equal(1, sickness.EffectSequence);
        SetTime(clock, 2);
        sickness.ApplyState(900, 1, 3.5f, .8f, true);
        Assert.Equal(1.5f, sickness.PukeDuration);
        sickness.ApplyState(0, 2, 0, 0, false);
        Assert.False(sickness.IsPuking);
        sickness.ApplyState(900, 1, 3.5f, .8f, true);
        Assert.False(sickness.IsSick);
        Assert.False(sickness.IsPuking);
    }

    private static void SetTime(SubsystemTime clock, double time) =>
        typeof(SubsystemTime).GetProperty(nameof(SubsystemTime.GameTime))!.SetValue(clock, time);

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private sealed class TestCharacter : ComponentCharacter
    {
        public override CharacterStats CharacterStats { get; } = new();
    }
}
