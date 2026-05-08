using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentInventory : ComponentInventoryBase
{
    public const int ShortInventorySlotsCount = 10;

    public override int ActiveSlotIndex
    {
        get;
        set => field = MathUtils.Clamp(value, 0, VisibleSlotsCount - 1);
    }

    public override int VisibleSlotsCount
    {
        get;
        set
        {
            value = MathUtils.Clamp(value, 0, 10);
            if (value == field)
            {
                return;
            }

            field = value;
            ActiveSlotIndex = ActiveSlotIndex;
            var componentFrame = Entity.FindComponent<ComponentFrame>();
            if (componentFrame == null)
            {
                return;
            }

            var position = componentFrame.Position + new Vector3(0f, 0.5f, 0f);
            var velocity = 1f * componentFrame.Rotation.GetForwardVector();
            for (var i = field; i < 10; i++)
            {
                DropSlotItems(i, position, velocity);
            }
        }
    } = 10;

    public override int GetSlotCapacity(int slotIndex, int value)
    {
        if (slotIndex >= VisibleSlotsCount && slotIndex < 10)
        {
            return 0;
        }

        return BlocksManager.Blocks[Terrain.ExtractContents(value)].GetMaxStacking(value);
    }

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
