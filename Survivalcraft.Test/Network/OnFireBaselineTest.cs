using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class OnFireBaselineTest
{
    [Theory]
    [InlineData(WorkType.Client)]
    [InlineData(WorkType.Server)]
    [InlineData(WorkType.Local)]
    public void LoadingRestoresFireWithoutSendingIgnitionEvent(WorkType mode)
    {
        var original = CommonLib.WorkType;
        var pending = CommonLib.Net.PendingPackageCount;
        CommonLib.WorkType = mode;
        try
        {
            var project = new BaselineProject();
            var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            typeof(Entity).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(entity, project);
            typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(entity, new List<Component> { new ComponentBody() });
            var fire = new ComponentOnFire();
            typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(fire, entity);
            var values = new ValuesDictionary();
            values.SetValue("FireDuration", 12.5f);

            fire.Load(values, new IdToEntityMap([]));

            Assert.True(fire.IsOnFire);
            var saved = new ValuesDictionary();
            fire.Save(saved, new EntityToIdMap([]));
            Assert.Equal(12.5f, saved.GetValue<float>("FireDuration"));
            Assert.Equal(pending, CommonLib.Net.PendingPackageCount);

            values.SetValue("FireDuration", 0f);
            fire.Load(values, new IdToEntityMap([]));
            Assert.False(fire.IsOnFire);
        }
        finally
        {
            CommonLib.WorkType = original;
        }
    }

    private sealed class BaselineProject : Project
    {
        public BaselineProject()
        {
            subsystems.AddRange([new SubsystemTime(), new SubsystemTerrain(), new SubsystemAudio(),
                new SubsystemAmbientSounds(), new SubsystemParticles()]);
        }
    }
}
