using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class FurnaceBaselineTest
{
    [Theory]
    [InlineData(12.5f, 2f, 0.75f)]
    [InlineData(0f, 0f, 0f)]
    public void LoadingBaselineRestoresFurnaceAndInventoryTogether(float fire, float heat, float progress)
    {
        var project = new FurnaceProject();
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        typeof(Entity).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, project);
        typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, new List<Component> { new ComponentBlockEntity() });
        var furnace = new ComponentFurnace();
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(furnace, entity);
        var slots = new ValuesDictionary();
        for (var index = 0; index < 6; index++)
        {
            var slot = new ValuesDictionary();
            slot.SetValue("Contents", 100 + index);
            slot.SetValue("Count", index + 1);
            slots.SetValue($"Slot{index}", slot);
        }

        var values = new ValuesDictionary();
        values.SetValue("Id", 42);
        values.SetValue("SlotsCount", 6);
        values.SetValue("Slots", slots);
        values.SetValue("FireTimeRemaining", fire);
        values.SetValue("HeatLevel", heat);
        values.SetValue("SmeltingProgress", progress);

        furnace.Load(values, new IdToEntityMap([]));

        Assert.Equal(fire, furnace.FireTimeRemaining);
        Assert.Equal(heat, furnace.HeatLevel);
        Assert.Equal(progress, furnace.SmeltingProgress);
        Assert.Same(furnace, project.FindSubsystem<SubsystemInventories>(true)!.GetInventoryById(42));
        Assert.Equal(6, furnace.SlotsCount);
        for (var index = 0; index < 6; index++)
        {
            Assert.Equal(100 + index, furnace.GetSlotValue(index));
            Assert.Equal(index + 1, furnace.GetSlotCount(index));
        }
    }

    [Fact]
    public void EntityBaselineContainsCompleteFurnaceState()
    {
        var furnace = new ComponentFurnace
        {
            FireTimeRemaining = 12.5f,
            HeatLevel = 2f,
            SmeltingProgress = 0.75f
        };
        var values = new ValuesDictionary();

        furnace.Save(values, new EntityToIdMap([]));

        Assert.Equal(furnace.FireTimeRemaining, values.GetValue<float>("FireTimeRemaining"));
        Assert.Equal(furnace.HeatLevel, values.GetValue<float>("HeatLevel"));
        Assert.Equal(furnace.SmeltingProgress, values.GetValue<float>("SmeltingProgress"));
    }

    private sealed class FurnaceProject : Project
    {
        public FurnaceProject()
        {
            subsystems.AddRange([new SubsystemInventories(), new SubsystemTerrain(), new SubsystemExplosions()]);
        }
    }
}
