using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game;
using Game.Components;
using Game.Network;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class HealthBaselineTest
{
    [Theory]
    [InlineData(0.35f, -1.0, "")]
    [InlineData(0f, 123.5, "Test death")]
    public void EntityBaselineRestoresHealthAirAndDeathWithoutSendingEvent(float health, double deathTime,
        string cause)
    {
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        typeof(Entity).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, new HealthProject());
        typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, new List<Component> { new ComponentCreature(), new ComponentOnFire() });
        var component = new ComponentHealth
        {
            Health = health,
            Air = 0.65f,
            DeathTime = deathTime < 0 ? null : deathTime,
            CauseOfDeath = cause
        };
        var values = new ValuesDictionary();
        values.SetValue("AttackResilience", 10f);
        values.SetValue("FallResilience", 10f);
        values.SetValue("FireResilience", 10f);
        values.SetValue("CorpseDuration", 60f);
        values.SetValue("BreathingMode", default(BreathingMode));
        values.SetValue("CanStrand", false);
        values.SetValue("AirCapacity", 10f);
        values.SetValue("DeathTime", -1.0);
        values.SetValue("CauseOfDeath", "");
        component.Save(values, new EntityToIdMap([]));
        var restored = new ComponentHealth();
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(restored, entity);
        var pending = CommonLib.Net.PendingPackageCount;

        restored.Load(values, new IdToEntityMap([]));

        Assert.Equal(health, restored.Health);
        Assert.Equal(component.Air, restored.Air);
        Assert.Equal(component.DeathTime, restored.DeathTime);
        Assert.Equal(cause, restored.CauseOfDeath);
        Assert.Equal(pending, CommonLib.Net.PendingPackageCount);
    }

    private sealed class HealthProject : Project
    {
        public HealthProject()
        {
            subsystems.AddRange([new SubsystemTime(), new SubsystemTimeOfDay(), new SubsystemTerrain(),
                new SubsystemParticles(), new SubsystemGameInfo { WorldSettings = new WorldSettings() },
                new SubsystemPickables()]);
        }
    }
}
