using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCraftingTable : ComponentInventoryBase
{
    private int _craftingGridSize;

    private readonly string[] _matchedIngredients = new string[9];

    private CraftingRecipe? _matchedRecipe;

    public int RemainsSlotIndex => SlotsCount - 1;

    public int ResultSlotIndex => SlotsCount - 2;

    public override int GetSlotCapacity(int slotIndex, int value)
    {
        return slotIndex < SlotsCount - 2 ? base.GetSlotCapacity(slotIndex, value) : 0;
    }

    public override void AddSlotItems(int slotIndex, int value, int count)
    {
        base.AddSlotItems(slotIndex, value, count);
        UpdateCraftingResult();
    }

    public override int RemoveSlotItems(int slotIndex, int count)
    {
        var num = 0;
        if (slotIndex == ResultSlotIndex)
        {
            if (_matchedRecipe != null)
            {
                if (_matchedRecipe.RemainsValue != 0 && _matchedRecipe.RemainsCount > 0)
                {
                    if (slots[RemainsSlotIndex].Count == 0 ||
                        slots[RemainsSlotIndex].Value == _matchedRecipe.RemainsValue)
                    {
                        var num2 = BlocksManager.Blocks[Terrain.ExtractContents(_matchedRecipe.RemainsValue)]
                            .GetMaxStacking(_matchedRecipe.RemainsValue) - slots[RemainsSlotIndex].Count;
                        count = MathUtils.Min(count, num2 / _matchedRecipe.RemainsCount * _matchedRecipe.ResultCount);
                    }
                    else
                    {
                        count = 0;
                    }
                }

                count = count / _matchedRecipe.ResultCount * _matchedRecipe.ResultCount;
                num = base.RemoveSlotItems(slotIndex, count);
                if (num > 0)
                {
                    for (var i = 0; i < 9; i++)
                    {
                        if (!string.IsNullOrEmpty(_matchedIngredients[i]))
                        {
                            var index = i % 3 + _craftingGridSize * (i / 3);
                            slots[index].Count =
                                MathUtils.Max(slots[index].Count - num / _matchedRecipe.ResultCount, 0);
                            OnSlotChange(index);
                        }
                    }

                    if (_matchedRecipe.RemainsValue != 0 && _matchedRecipe.RemainsCount > 0)
                    {
                        slots[RemainsSlotIndex].Value = _matchedRecipe.RemainsValue;
                        slots[RemainsSlotIndex].Count +=
                            num / _matchedRecipe.ResultCount * _matchedRecipe.RemainsCount;
                        OnSlotChange(RemainsSlotIndex);
                    }

                    var componentPlayer = FindInteractingPlayer();
                    componentPlayer?.PlayerStats?.ItemsCrafted += num;
                }
            }
        }
        else
        {
            num = base.RemoveSlotItems(slotIndex, count);
        }

        UpdateCraftingResult();
        return num;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _craftingGridSize = (int)MathUtils.Sqrt(SlotsCount - 2);
    }

    public void UpdateCraftingResult()
    {
        var num = int.MaxValue;
        for (var i = 0; i < _craftingGridSize; i++)
        {
            for (var j = 0; j < _craftingGridSize; j++)
            {
                var num2 = i + j * 3;
                var slotIndex = i + j * _craftingGridSize;
                var slotValue = GetSlotValue(slotIndex);
                var num3 = Terrain.ExtractContents(slotValue);
                var num4 = Terrain.ExtractData(slotValue);
                var slotCount = GetSlotCount(slotIndex);
                if (slotCount > 0)
                {
                    var block = BlocksManager.Blocks[num3];
                    _matchedIngredients[num2] = block.CraftingId + ":" + num4.ToString(CultureInfo.InvariantCulture);
                    num = MathUtils.Min(num, slotCount);
                }
                else
                {
                    _matchedIngredients[num2] = string.Empty;
                }
            }
        }

        var componentPlayer = FindInteractingPlayer();
        var craftingRecipe = CraftingRecipesManager.FindMatchingRecipe(
            Project.FindSubsystem<SubsystemTerrain>(true)!,
            _matchedIngredients,
            0f,
            componentPlayer
        );
        if (craftingRecipe != null && craftingRecipe.ResultValue != 0)
        {
            _matchedRecipe = craftingRecipe;
            slots[ResultSlotIndex].Value = craftingRecipe.ResultValue;
            slots[ResultSlotIndex].Count = craftingRecipe.ResultCount * num;
        }
        else
        {
            _matchedRecipe = null;
            slots[ResultSlotIndex].Value = 0;
            slots[ResultSlotIndex].Count = 0;
        }

        OnSlotChange(ResultSlotIndex);
        if (craftingRecipe != null && !string.IsNullOrEmpty(craftingRecipe.Message))
        {
            componentPlayer?.ComponentGui.DisplaySmallMessage(craftingRecipe.Message, Color.White, true, true);
        }
    }
}
