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
        var removedCount = 0;
        if (slotIndex == ResultSlotIndex)
        {
            if (_matchedRecipe != null)
            {
                if (_matchedRecipe.RemainsValue != 0 && _matchedRecipe.RemainsCount > 0)
                {
                    if (slots[RemainsSlotIndex].Count == 0 ||
                        slots[RemainsSlotIndex].Value == _matchedRecipe.RemainsValue)
                    {
                        var remainsCapacity = WorldContentBinding.For(Project).Catalog
                                                  .Blocks[Terrain.ExtractContents(_matchedRecipe.RemainsValue)]
                                                  .GetMaxStacking(_matchedRecipe.RemainsValue) -
                                              slots[RemainsSlotIndex].Count;
                        count = MathUtils.Min(
                            count,
                            remainsCapacity / _matchedRecipe.RemainsCount * _matchedRecipe.ResultCount
                        );
                    }
                    else
                    {
                        count = 0;
                    }
                }

                count = count / _matchedRecipe.ResultCount * _matchedRecipe.ResultCount;
                removedCount = base.RemoveSlotItems(slotIndex, count);
                if (removedCount > 0)
                {
                    for (var i = 0; i < 9; i++)
                    {
                        if (!string.IsNullOrEmpty(_matchedIngredients[i]))
                        {
                            var index = i % 3 + _craftingGridSize * (i / 3);
                            slots[index].Count =
                                MathUtils.Max(slots[index].Count - removedCount / _matchedRecipe.ResultCount, 0);
                            OnSlotChange(index);
                        }
                    }

                    if (_matchedRecipe.RemainsValue != 0 && _matchedRecipe.RemainsCount > 0)
                    {
                        slots[RemainsSlotIndex].Value = _matchedRecipe.RemainsValue;
                        slots[RemainsSlotIndex].Count +=
                            removedCount / _matchedRecipe.ResultCount * _matchedRecipe.RemainsCount;
                        OnSlotChange(RemainsSlotIndex);
                    }

                    var componentCharacter = FindInteractingPlayer();
                    componentCharacter?.CharacterStats?.ItemsCrafted += removedCount;
                }
            }
        }
        else
        {
            removedCount = base.RemoveSlotItems(slotIndex, count);
        }

        UpdateCraftingResult();
        return removedCount;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _craftingGridSize = (int)MathUtils.Sqrt(SlotsCount - 2);
    }

    public void UpdateCraftingResult()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        var availableBatches = int.MaxValue;
        for (var i = 0; i < _craftingGridSize; i++)
        {
            for (var j = 0; j < _craftingGridSize; j++)
            {
                var ingredientIndex = i + j * 3;
                var slotIndex = i + j * _craftingGridSize;
                var slotValue = GetSlotValue(slotIndex);
                var contents = Terrain.ExtractContents(slotValue);
                var itemData = Terrain.ExtractData(slotValue);
                var slotCount = GetSlotCount(slotIndex);
                if (slotCount > 0)
                {
                    var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
                    _matchedIngredients[ingredientIndex] =
                        block.CraftingId + ":" + itemData.ToString(CultureInfo.InvariantCulture);
                    availableBatches = MathUtils.Min(availableBatches, slotCount);
                }
                else
                {
                    _matchedIngredients[ingredientIndex] = string.Empty;
                }
            }
        }

        var componentCharacter = FindInteractingPlayer();
        var craftingRecipe = WorldContentBinding.For(Project).Catalog.Recipes.FindMatchingRecipe(
            Project.FindSubsystem<SubsystemTerrain>(true)!,
            _matchedIngredients,
            0f,
            componentCharacter?.CharacterData.Level
        );
        if (craftingRecipe != null && craftingRecipe.ResultValue != 0)
        {
            _matchedRecipe = craftingRecipe;
            slots[ResultSlotIndex].Value = craftingRecipe.ResultValue;
            slots[ResultSlotIndex].Count = craftingRecipe.ResultCount * availableBatches;
        }
        else
        {
            _matchedRecipe = null;
            slots[ResultSlotIndex].Value = 0;
            slots[ResultSlotIndex].Count = 0;
        }

        OnSlotChange(ResultSlotIndex);
        if (craftingRecipe != null && craftingRecipe.HasMessage)
        {
            PublishRecipeMessage(
                componentCharacter,
                craftingRecipe.CreateFeedbackText(),
                craftingRecipe.IsDisabledResult ? GameMessageTone.Error : GameMessageTone.Normal
            );
        }
    }
}
