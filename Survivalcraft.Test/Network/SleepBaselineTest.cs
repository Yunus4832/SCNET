using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Network;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SleepBaselineTest
{
    [Theory]
    [InlineData(0.0, false)]
    [InlineData(123.25, true)]
    public void LoadingBaselineRestoresSleepWithoutSendingAnotherSleepEvent(double startTime, bool manualWakeup)
    {
        var project = new SleepProject();
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        typeof(Entity).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, project);
        var player = new ComponentPlayer { ComponentHealth = new ComponentHealth() };
        typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, new List<Component> { player });
        var sleep = new ComponentSleep();
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(sleep, entity);
        var values = new ValuesDictionary();
        values.SetValue("SleepStartTime", startTime);
        values.SetValue("AllowManualWakeUp", manualWakeup);
        var pending = CommonLib.Net.PendingPackageCount;

        sleep.Load(values, new IdToEntityMap([]));

        Assert.Equal(startTime != 0.0, sleep.IsSleeping);
        Assert.Equal(startTime == 0.0 ? 0f : 1f, sleep.SleepFactor);
        Assert.Equal(pending, CommonLib.Net.PendingPackageCount);
        var saved = new ValuesDictionary();
        sleep.Save(saved, new EntityToIdMap([]));
        Assert.Equal(startTime, saved.GetValue<double>("SleepStartTime"));
        Assert.Equal(manualWakeup, saved.GetValue<bool>("AllowManualWakeUp"));
    }

    private sealed class SleepProject : Project
    {
        public SleepProject()
        {
            subsystems.AddRange([new SubsystemPlayers(), new SubsystemTime(), new SubsystemUpdate(),
                new SubsystemGameInfo(), new SubsystemTimeOfDay(), new SubsystemTerrain()]);
        }
    }
}
