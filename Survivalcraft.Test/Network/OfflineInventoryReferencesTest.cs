using EntitySystem.TemplatesDatabase;

using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class OfflineInventoryReferencesTest
{
    [Theory]
    [InlineData("CreativeInventory", null, 1)]
    [InlineData("Inventory", 4, 1)]
    [InlineData("Inventory", 0, 0)]
    public void SavedSlotsRetainOnlyNonemptyInventoryReferences(string componentName, int? count, int expected)
    {
        var slot = new ValuesDictionary();
        slot.SetValue("Contents", 67109091);
        if (count.HasValue)
        {
            slot.SetValue("Count", count.Value);
        }

        var slots = new ValuesDictionary();
        slots.SetValue("Slot0", slot);
        var component = new ValuesDictionary();
        component.SetValue("Slots", slots);
        var overrides = new ValuesDictionary();
        overrides.SetValue(componentName, component);
        var entity = new ValuesDictionary();
        entity.SetValue("Overrides", overrides);

        var values = SubsystemPlayers.GetSavedInventoryValues(entity).ToArray();

        Assert.Equal(expected, values.Length);
        Assert.All(values, value => Assert.Equal(67109091, value));
    }

    [Fact]
    public void ComponentsWithoutSlotsHaveNoInventoryReferences()
    {
        Assert.Empty(SubsystemPlayers.GetSavedInventoryValues(new ValuesDictionary()));
    }
}
