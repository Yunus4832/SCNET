using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentInventory : ComponentInventoryBase, IPagedInventory
{
    public int HotbarSlotsCount => PlayerInventoryLayout.RegularHotbarSlotsCount;

    public override int ActiveSlotIndex
    {
        get;
        set => field = MathUtils.Clamp(value, 0, HotbarSlotsCount - 1);
    }

    public override int VisibleSlotsCount
    {
        get;
        set
        {
            value = MathUtils.Clamp(value, 1, HotbarSlotsCount);
            if (value == field)
            {
                return;
            }

            field = value;
            ActiveSlotIndex = ActiveSlotIndex;
        }
    } = PlayerInventoryLayout.RegularHotbarSlotsCount;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        ActiveSlotIndex = valuesDictionary.GetValue<int>("ActiveSlotIndex");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        base.Save(valuesDictionary, entityToIdMap);
        valuesDictionary.SetValue("ActiveSlotIndex", ActiveSlotIndex);
    }
}
