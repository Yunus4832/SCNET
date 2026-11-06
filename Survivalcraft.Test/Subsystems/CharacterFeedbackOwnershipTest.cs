using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.Rendering;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class CharacterFeedbackOwnershipTest
{
    [Fact]
    public void DamageReplicaReceivesEventsWithoutRunningAnUpdateLoop()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var damage = new ComponentDamage { HitPoints = 1 };
        project.InitializeComponent(damage);
        var attacked = 0;
        var destroyed = 0;
        damage.Attacked += () => attacked++;
        damage.Destroyed += () => destroyed++;
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(damage, WorldRuntimeContext.For(project)));
        damage.Update(1f);
        damage.ApplyDamageState(.5f, false, true);
        Assert.Equal(.5f, damage.HitPoints);
        Assert.Equal(1, attacked);
        damage.ApplyDamageState(0, true, false);
        damage.ApplyDamageState(0, true, false);
        Assert.Equal(1, destroyed);
        WorldRuntimeContext.For(project).Close();
        damage.ApplyDamageState(.8f, false, true);
        Assert.Equal(0, damage.HitPoints);
        Assert.Equal(1, attacked);
    }

    [Fact]
    public void ClosedAuthorityDamageCannotReachAnySimulationDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var damage = new ComponentDamage { HitPoints = 1 };
        project.InitializeComponent(damage);
        WorldRuntimeContext.For(project).Close();
        damage.Update(1f);
        damage.Damage(.5f);
        Assert.Equal(1f, damage.HitPoints);
    }

    [Fact]
    public void AuthorityFeedbackLoadsWithoutGraphicsOrAudioDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        foreach (var view in new Component[] { new ComponentCharacterPresentation() })
        {
            project.InitializeComponent(view);
            view.Load(new ValuesDictionary(), null!);
            view.OnEntityAdded();
            if (view is IDrawable drawable) { drawable.Draw(null!, 0); }

            view.OnEntityRemoved();
            view.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CharacterFeedbackUnsubscribesAndRejectsOldCallbacks(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var character = new ComponentCharacter();
        var view = new ComponentCharacterPresentation();
        project.InitializeComponent(view);
        SetField(view, "_character", character);
        view.OnEntityAdded();
        view.OnEntityAdded();
        var speed = (Delegate)ReadField(character, "SteedSpeedFeedback")!;
        var pick = (Delegate)ReadField(character, "CreativeBlockPicked")!;
        var aim = (Delegate)ReadField(character, "AimHintRequested")!;
        Assert.Single(speed.GetInvocationList());
        Assert.Single(pick.GetInvocationList());
        Assert.Single(aim.GetInvocationList());
        if (dispose) { view.Dispose(); }
        else { view.OnEntityRemoved(); }

        Assert.Null(ReadField(character, "SteedSpeedFeedback"));
        Assert.Null(ReadField(character, "CreativeBlockPicked"));
        Assert.Null(ReadField(character, "AimHintRequested"));
        speed.DynamicInvoke(true);
        pick.DynamicInvoke(3);
        aim.DynamicInvoke();
        view.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingAimHintIsDeduplicatedAndCannotSurviveLifecycleOrAimCancellation(bool remove)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var character = new ComponentCharacter();
        project.InitializeComponent(character);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, true);
        character.CharacterData = data;
        var view = new ComponentCharacterPresentation();
        project.InitializeComponent(view);
        SetField(view, "_character", character);
        view.OnEntityAdded();
        var aim = (Delegate)ReadField(character, "AimHintRequested")!;
        aim.DynamicInvoke();
        aim.DynamicInvoke();
        var runtime = WorldRuntimeContext.For(project);
        Assert.Single(((IEnumerable)ReadField(runtime, "_delayedActions")!).Cast<object>());
        if (remove)
        {
            // Aiming and dependencies are intentionally inconsistent: a stale callback must not read them.
            SetField(character, "_aim", new Ray3(Vector3.Zero, Vector3.UnitZ));
            view.OnEntityRemoved();
            view.OnEntityAdded();
        }

        runtime.RunTimeDelayedExecutions(double.MaxValue);
        Assert.False((bool)ReadField(view, "_aimHintPending")!);
        Assert.False((bool)ReadField(view, "_aimHintIssued")!);
        view.Dispose();
    }

    [Fact]
    public void GlowPointsAreAddedOnceAndReleasedOnRemovalAndDisposal()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var glow = new SubsystemGlow();
        var eyes = new ComponentGlowingEyes();
        project.InitializeComponent(eyes);
        SetField(eyes, "_subsystemGlow", glow);
        var points = (IDictionary)ReadField(glow, "_glowPoints")!;
        eyes.OnEntityAdded();
        eyes.OnEntityAdded();
        Assert.Equal(2, points.Count);
        eyes.OnEntityRemoved();
        Assert.Empty(points);
        eyes.OnEntityAdded();
        Assert.Equal(2, points.Count);
        eyes.Dispose();
        eyes.Dispose();
        Assert.Empty(points);
        eyes.Draw(null!, 0);
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
