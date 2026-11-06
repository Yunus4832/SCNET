using System.Reflection;

using Game.Components;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class HealthAndDamageReplicaTest
{
    [Fact]
    public void ClientAppliesDeathFactsWithoutInvokingInjuryRulesOrCreatingStatisticsAndDrops()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var health = new ComponentHealth { Health = 1f };
        project.InitializeComponent(health);
        // No rule dependencies exist; applying a snapshot must not execute the injury/death simulation.
        health.ApplyInjuryState(0f, LocalizedText.Literal("Authoritative cause"), 123d, null);
        Assert.Equal(0f, health.Health);
        Assert.Equal(LocalizedText.Literal("Authoritative cause"), health.CauseOfDeath);
        Assert.Equal(123d, health.DeathTime);
    }

    [Fact]
    public void AuthorityRejectsReplicaHealthAndDamageUpdates()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var health = new ComponentHealth { Health = 1f };
        var damage = new ComponentDamage { HitPoints = 1f };
        project.InitializeComponent(health);
        project.InitializeComponent(damage);
        health.ApplyInjuryState(0f, LocalizedText.Literal("Untrusted"), 123d, null);
        damage.ApplyDamageState(0f, true, true);
        Assert.Equal(1f, health.Health);
        Assert.Null(health.DeathTime);
        Assert.Equal(1f, damage.HitPoints);
    }

    [Fact]
    public void ReplicaDestructionNotificationIsExplicitAndIdempotentAndDoesNotRetireTheEntity()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var damage = new ComponentDamage { HitPoints = 1f };
        project.InitializeComponent(damage);
        damage.Entity.IsAddedToProject = true;
        var destroyed = 0;
        var attacked = 0;
        damage.Destroyed += () => destroyed++;
        damage.Attacked += () => attacked++;
        damage.ApplyDamageState(0f, false, true);
        damage.Update(1f);
        Assert.Equal(1, attacked);
        Assert.Equal(0, destroyed);
        Assert.True(damage.IsAddedToProject);
        damage.ApplyDamageState(0f, true, false);
        damage.ApplyDamageState(0f, true, false);
        Assert.Equal(1, destroyed);
        Assert.True(damage.IsAddedToProject);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingOrDisposingTheDamagePresentationUnsubscribesFromFutureStateEvents(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var damage = new ComponentDamage { HitPoints = 1f };
        project.InitializeComponent(damage);
        var presentation = new ComponentDamagePresentation();
        presentation.Initialize(damage.Entity, damage.ValuesDictionary);
        typeof(ComponentDamagePresentation).GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(presentation, damage);
        presentation.OnEntityAdded();
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        // A lingering callback would access the absent rendering dependencies and fail.
        damage.ApplyDamageState(0f, true, true);
        presentation.Dispose();
    }
}
