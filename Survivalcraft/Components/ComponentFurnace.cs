using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentFurnace : ComponentInventoryBase, IUpdateable
{
    private bool _isRun;

    private ComponentBlockEntity _componentBlockEntity = null!;

    public float FireTimeRemaining;

    private int _furnaceSize;

    private readonly string[] _matchedIngredients = new string[9];

    private CraftingRecipe? _smeltingRecipe;

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private bool _updateSmeltingRecipe;

    public int RemainsSlotIndex => SlotsCount - 1;

    public int ResultSlotIndex => SlotsCount - 2;

    public int FuelSlotIndex => SlotsCount - 3;

    public float HeatLevel { get; set; }

    public float SmeltingProgress { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var coordinates = _componentBlockEntity.Coordinates;
        if (HeatLevel > 0f)
        {
            FireTimeRemaining = MathUtils.Max(0f, FireTimeRemaining - dt);
            if (FireTimeRemaining == 0f)
            {
                HeatLevel = 0f;
            }
        }

        if (_updateSmeltingRecipe)
        {
            _updateSmeltingRecipe = false;
            var heatLevel = 0f;
            if (HeatLevel > 0f)
            {
                heatLevel = HeatLevel;
            }
            else
            {
                var slot = slots[FuelSlotIndex];
                if (slot.Count > 0)
                {
                    var num = Terrain.ExtractContents(slot.Value);
                    heatLevel = BlocksManager.Blocks[num].FuelHeatLevel;
                }
            }

            var craftingRecipe = FindSmeltingRecipe(heatLevel);
            if (craftingRecipe != _smeltingRecipe)
            {
                _smeltingRecipe = craftingRecipe != null && craftingRecipe.ResultValue != 0 ? craftingRecipe : null;
                SmeltingProgress = 0f;
            }
        }

        if (_smeltingRecipe == null)
        {
            HeatLevel = 0f;
            FireTimeRemaining = 0f;
        }

        if (_smeltingRecipe != null && FireTimeRemaining <= 0f)
        {
            var slot2 = slots[FuelSlotIndex];
            if (slot2.Count > 0)
            {
                var num2 = Terrain.ExtractContents(slot2.Value);
                var block = BlocksManager.Blocks[num2];
                if (block.GetExplosionPressure(slot2.Value) > 0f)
                {
                    slot2.Count = 0;
                    _subsystemExplosions.TryExplodeBlock(
                        coordinates.X,
                        coordinates.Y,
                        coordinates.Z,
                        slot2.Value,
                        _componentBlockEntity.OwnPlayerData
                    );
                }
                else if (block.FuelHeatLevel > 0f)
                {
                    slot2.Count--;
                    FireTimeRemaining = block.FuelFireDuration;
                    HeatLevel = block.FuelHeatLevel;
                }

                OnSlotChange(FuelSlotIndex);
            }
        }

        if (FireTimeRemaining <= 0f)
        {
            _smeltingRecipe = null;
            SmeltingProgress = 0f;
        }

        if (_smeltingRecipe != null)
        {
            SmeltingProgress = MathUtils.Min(SmeltingProgress + 0.15f * dt, 1f);
            _isRun = true;
            if (SmeltingProgress >= 1f)
            {
                for (var i = 0; i < _furnaceSize; i++)
                {
                    if (slots[i].Count > 0)
                    {
                        slots[i].Count--;
                        OnSlotChange(i);
                    }
                }

                slots[ResultSlotIndex].Value = _smeltingRecipe.ResultValue;
                slots[ResultSlotIndex].Count += _smeltingRecipe.ResultCount;
                if (_smeltingRecipe.RemainsValue != 0 && _smeltingRecipe.RemainsCount > 0)
                {
                    slots[RemainsSlotIndex].Value = _smeltingRecipe.RemainsValue;
                    slots[RemainsSlotIndex].Count += _smeltingRecipe.RemainsCount;
                    OnSlotChange(RemainsSlotIndex);
                }

                OnSlotChange(ResultSlotIndex);
                _smeltingRecipe = null;
                SmeltingProgress = 0f;
                _updateSmeltingRecipe = true;
            }
        }

        var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(coordinates.X, coordinates.Z, false);
        if (chunkAtCell is { MainThreadState: TerrainChunkState.Valid })
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(coordinates.X, coordinates.Y, coordinates.Z);
            var contents = Terrain.ExtractContents(cellValue);
            if (HeatLevel > 0.0f)
            {
                if (contents != 65)
                {
                    _subsystemTerrain.ChangeCell(coordinates.X, coordinates.Y, coordinates.Z,
                        Terrain.ReplaceContents(cellValue, 65));
                }
            }
            else
            {
                if (contents != 64)
                {
                    _subsystemTerrain.ChangeCell(coordinates.X, coordinates.Y, coordinates.Z,
                        Terrain.ReplaceContents(cellValue, 64));
                }
            }
        }

        if (!_isRun || CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (_smeltingRecipe != null || FireTimeRemaining != 0)
        {
            return;
        }

        _isRun = false;
        CommonLib.Net.QueuePackage(
            new ComponentFurnacePackage(
                Entity.EntityId,
                FireTimeRemaining,
                SmeltingProgress,
                HeatLevel
            )
        );
    }

    public override int GetSlotCapacity(int slotIndex, int value)
    {
        if (slotIndex != FuelSlotIndex || BlocksManager.Blocks[Terrain.ExtractContents(value)].FuelHeatLevel > 0f)
        {
            return base.GetSlotCapacity(slotIndex, value);
        }

        return 0;
    }

    public override void AddSlotItems(int slotIndex, int value, int count)
    {
        _updateSmeltingRecipe = true;
        base.AddSlotItems(slotIndex, value, count);
    }

    public override int RemoveSlotItems(int slotIndex, int count)
    {
        _updateSmeltingRecipe = true;
        return base.RemoveSlotItems(slotIndex, count);
    }

    public override void SetSlotValue(int slotIndex, object obj)
    {
        _updateSmeltingRecipe = true;
        base.SetSlotValue(slotIndex, obj);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
        _componentBlockEntity = Entity.FindComponent<ComponentBlockEntity>(true)!;
        _furnaceSize = SlotsCount - 3;
        if (_furnaceSize is < 1 or > 3)
        {
            throw new InvalidOperationException("Invalid furnace size.");
        }

        FireTimeRemaining = valuesDictionary.GetValue<float>("FireTimeRemaining");
        HeatLevel = valuesDictionary.GetValue<float>("HeatLevel");
        _updateSmeltingRecipe = true;
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        base.Save(valuesDictionary, entityToIdMap);
        valuesDictionary.SetValue("FireTimeRemaining", FireTimeRemaining);
        valuesDictionary.SetValue("HeatLevel", HeatLevel);
    }

    public CraftingRecipe? FindSmeltingRecipe(float heatLevel)
    {
        if (!(heatLevel > 0f))
        {
            return null;
        }

        for (var i = 0; i < _furnaceSize; i++)
        {
            var slotValue = GetSlotValue(i);
            var num = Terrain.ExtractContents(slotValue);
            var num2 = Terrain.ExtractData(slotValue);
            if (GetSlotCount(i) > 0)
            {
                var block = BlocksManager.Blocks[num];
                _matchedIngredients[i] = block.CraftingId + ":" + num2.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                _matchedIngredients[i] = string.Empty;
            }
        }

        var componentPlayer = FindInteractingPlayer();
        var craftingRecipe = CraftingRecipesManager.FindMatchingRecipe(
            _subsystemTerrain,
            _matchedIngredients,
            heatLevel,
            componentPlayer
        );
        if (craftingRecipe != null && craftingRecipe.ResultValue != 0)
        {
            if (craftingRecipe.RequiredHeatLevel <= 0f)
            {
                craftingRecipe = null;
            }

            if (craftingRecipe != null)
            {
                var slot = slots[ResultSlotIndex];
                var num3 = Terrain.ExtractContents(craftingRecipe.ResultValue);
                if (slot.Count != 0 && (craftingRecipe.ResultValue != slot.Value ||
                                        craftingRecipe.ResultCount + slot.Count >
                                        BlocksManager.Blocks[num3].MaxStacking))
                {
                    craftingRecipe = null;
                }
            }

            if (craftingRecipe != null && craftingRecipe.RemainsValue != 0 && craftingRecipe.RemainsCount > 0)
            {
                if (slots[RemainsSlotIndex].Count == 0 ||
                    slots[RemainsSlotIndex].Value == craftingRecipe.RemainsValue)
                {
                    if (BlocksManager.Blocks[Terrain.ExtractContents(craftingRecipe.RemainsValue)].MaxStacking -
                        slots[RemainsSlotIndex].Count < craftingRecipe.RemainsCount)
                    {
                        craftingRecipe = null;
                    }
                }
                else
                {
                    craftingRecipe = null;
                }
            }
        }

        if (craftingRecipe != null && !string.IsNullOrEmpty(craftingRecipe.Message))
        {
            componentPlayer?.ComponentGui.DisplaySmallMessage(craftingRecipe.Message, Color.White, true, true);
        }

        return craftingRecipe;
    }
}
