using System.Reflection;

using Game.Characters;
using Game.Components;
using Game.Network.Handling;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class SurvivalAuthorityBoundaryTest
{
    [Fact]
    public void ClientDiseaseEntryPointsCannotCreateRulesOrStatistics()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var flu = new ComponentFlu();
        project.InitializeComponent(flu);
        flu.StartFlu();
        flu.FluEffect();
        flu.Cough();
        flu.Sneeze();
        Assert.False(flu.HasFlu);
        Assert.Equal(0, flu.EffectSequence);
        flu.ApplyState(
            new ComponentFluPackage
            {
                EffectSequence = 1, FluDuration = 900, CoughDuration = 4,
                BlackoutDuration = 2, PackageEventType = ComponentFluPackage.EventType.FluEffect, LastWasCough = true
            }
        );
        Assert.Equal(900, flu.FluDuration);
        Assert.Equal(4, flu.CoughDuration);
        flu.ApplyState(new ComponentFluPackage { EffectSequence = 0 });
        Assert.Equal(900, flu.FluDuration);
        flu.CoughDuration = 2;
        flu.ApplyState(new ComponentFluPackage { EffectSequence = 1, FluDuration = 899, CoughDuration = 4 });
        Assert.Equal(2, flu.CoughDuration);
    }

    [Fact]
    public void WakeIntentMustMatchAuthorityEpisodeAndPolicyAndMinimumDuration()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var sleep = new ComponentSleep();
        project.InitializeComponent(sleep);
        var info = new SubsystemGameInfo { TotalElapsedGameTime = 105 };
        Set(sleep, "_info", info);
        typeof(ComponentSleep).GetProperty(nameof(ComponentSleep.SleepStartTime))!.SetValue(sleep, (double?)100);
        typeof(ComponentSleep).GetProperty(nameof(ComponentSleep.AllowManualWakeUp))!.SetValue(sleep, true);
        Assert.False(sleep.AcceptsWakeUpRequest(100));
        info.TotalElapsedGameTime = 111;
        Assert.False(sleep.AcceptsWakeUpRequest(99));
        Assert.True(sleep.AcceptsWakeUpRequest(100));
        typeof(ComponentSleep).GetProperty(nameof(ComponentSleep.AllowManualWakeUp))!.SetValue(sleep, false);
        Assert.False(sleep.AcceptsWakeUpRequest(100));
    }

    [Fact]
    public void ClientSleepIsStateApplicationNotPermissionOrStatisticMutation()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var sleep = new ComponentSleep();
        project.InitializeComponent(sleep);
        sleep.Sleep(false);
        Assert.False(sleep.IsSleeping);
        sleep.ApplySleepState(100, false);
        sleep.WakeUp();
        Assert.True(sleep.IsSleeping);
        Assert.False(sleep.AllowManualWakeUp);
        sleep.ApplySleepState(null, false, SleepWakeReason.Wet);
        Assert.False(sleep.IsSleeping);
    }

    [Fact]
    public void AuthorityIgnoresUntrustedVitalSnapshots()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var vital = new ComponentVitalStats { Stamina = .2f, Food = .3f };
        project.InitializeComponent(vital);
        vital.ApplyState(new VitalStatePackage { Stamina = 1, Food = 1 });
        Assert.Equal(.2f, vital.Stamina);
        Assert.Equal(.3f, vital.Food);
        // Direction rejection must happen before entity lookup, inventory or world dependencies are touched.
        new ComponentVitalStatPackageHandler().Handle(
            new VitalStatePackage { Stamina = 1 },
            Game.Network.WorldNetworkBinding.For(project).RequireNode().CreateReceiveContext()
        );
        Assert.Equal(.2f, vital.Stamina);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedStaminaCalculationChargesMovementAndCannotExceedBounds(bool swimming)
    {
        var idle = StaminaSimulation.Step(.5f, .1f, 0, false, swimming, 1, false);
        var moving = StaminaSimulation.Step(.5f, .1f, 1, false, swimming, 1, false);
        var jumping = StaminaSimulation.Step(.5f, .1f, 1, true, swimming, 1, false);
        Assert.True(moving < idle);
        Assert.True(jumping < moving);
        Assert.InRange(StaminaSimulation.Step(1, .1f, 0, false, swimming, 1, false), 0, 1);
        Assert.InRange(StaminaSimulation.Step(0, 10, 1, true, swimming, 1, true), 0, 1);
    }

    private static void Set(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
}
