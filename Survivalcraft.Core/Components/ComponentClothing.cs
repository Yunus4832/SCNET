using Engine.Core.Serialization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentClothing : Component, IUpdateable, IInventory
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    public event Action? ClothesChanged;

    private readonly Dictionary<ClothingSlot, List<int>> _clothes = new();

    private readonly List<int> _clothesList = [];

    private ComponentBody _componentBody = null!;

    private ComponentLocomotion _componentLocomotion = null!;

    private ComponentCharacter _componentCharacter = null!;

    private ComponentVitalStats _componentVitalStats = null!;

    private float _densityModifierApplied;

    private double? _lastTotalElapsedGameTime;

    private readonly Random _random = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTime _subsystemTime = null!;

    public float Insulation { get; set; }

    public ClothingSlot LeastInsulatedSlot { get; set; }

    public float SteedMovementSpeedFactor { get; set; }

    public int Id { get; private set; }

    Project IInventory.Project => Project;

    public int SlotsCount => 4;

    public int VisibleSlotsCount { get => SlotsCount; set { } }

    public int ActiveSlotIndex { get => -1; set { } }

    public virtual void OnSlotChange(int slotIndex)
    {
        SubsystemInventories.PushSyncItem(this, slotIndex);
    }

    public virtual int GetSlotValue(int slotIndex)
    {
        return GetClothes((ClothingSlot)slotIndex).LastOrDefault();
    }

    public virtual int GetSlotCount(int slotIndex)
    {
        if (GetClothes((ClothingSlot)slotIndex).Count <= 0)
        {
            return 0;
        }

        return 1;
    }

    public virtual int GetSlotCapacity(int slotIndex, int value)
    {
        return 0;
    }

    public virtual int GetSlotProcessCapacity(int slotIndex, int value)
    {
        var block = WorldContentBinding.For(Project).Catalog.Blocks[Terrain.ExtractContents(value)];
        if (block.GetNutritionalValue(value) > 0f)
        {
            return 1;
        }

        if (block is ClothingBlock && CanWearClothing(value))
        {
            return 1;
        }

        return 0;
    }

    public virtual void AddSlotItems(int slotIndex, int value, int count)
    {
    }

    public virtual void ProcessSlotItems(
        IInventory sourceInventory,
        int sourceSlotIndex,
        int slotIndex,
        int value,
        int count,
        int processCount,
        out int processedValue,
        out int processedCount
    )
    {
        processedCount = 0;
        processedValue = 0;
        if (!WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).IsAuthority ||
            processCount != 1)
        {
            return;
        }

        var block = WorldContentBinding.For(Project).Catalog.Blocks[Terrain.ExtractContents(value)];
        if (block.GetNutritionalValue(value) > 0f)
        {
            if (block is BucketBlock)
            {
                processedValue = Terrain.MakeBlockValue(90, Terrain.ExtractData(value));
                processedCount = 1;
            }

            if (count > 1 && processedCount > 0 && processedValue != value)
            {
                processedValue = value;
                processedCount = processCount;
            }
            else if (!_componentVitalStats.Eat(sourceInventory, sourceSlotIndex, value))
            {
                processedValue = value;
                processedCount = processCount;
            }
        }

        if (block is ClothingBlock)
        {
            var clothingData = WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(value)]
                .GetClothingData(Terrain.ExtractData(value));
            var list = new List<int>(GetClothes(clothingData.Slot))
            {
                value
            };
            SetClothes(clothingData.Slot, list);
            OnSlotChange((int)clothingData.Slot);
        }
    }

    public virtual void DropAllItems(Vector3 position)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var random = new Random();
        var subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        for (var i = 0; i < SlotsCount; i++)
        {
            var x = GetClothes((ClothingSlot)i).Count;
            for (var j = 0; j < x; j++)
            {
                var slotValue = GetSlotValue(i);
                var count = RemoveSlotItems(i, 1);
                var value = random.Float(5f, 10f) *
                            Vector3.Normalize(
                                new Vector3(
                                    random.Float(-1f, 1f),
                                    random.Float(1f, 2f),
                                    random.Float(-1f, 1f)
                                )
                            );
                subsystemPickables.AddPickable(slotValue, count, position, value, null);
            }
        }
    }

    public virtual void SetSlotValue(int slotIndex, object obj)
    {
        var list = (List<int>)obj;
        var slot = (ClothingSlot)slotIndex;
        SetClothes(slot, list);
    }

    public virtual bool AddNetSlotItems(int slotIndex, int value, int count)
    {
        return false;
    }

    public virtual int RemoveSlotItems(int slotIndex, int count)
    {
        count = RemoveNetSlotItems(slotIndex, count);
        OnSlotChange(slotIndex);
        return count;
    }

    public virtual int RemoveNetSlotItems(int slotIndex, int count)
    {
        if (count == 1)
        {
            var list = new List<int>(GetClothes((ClothingSlot)slotIndex));
            if (list.Count > 0)
            {
                list.RemoveAt(list.Count - 1);
                SetClothes((ClothingSlot)slotIndex, list);
                return 1;
            }
        }

        return 0;
    }

    public virtual void DropSlotItems(int slotIndex, Vector3 position, Vector3 velocity)
    {
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public virtual void Update(float dt)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        //计算等级需求
        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
            _subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled &&
            _subsystemTime.PeriodicGameTimeEvent(0.5, 0.0))
        {
            foreach (var enumValue in EnumUtils.GetEnumValues(typeof(ClothingSlot)))
            {
                var flag = false;
                _clothesList.Clear();
                _clothesList.AddRange(GetClothes((ClothingSlot)enumValue));
                var num = 0;
                while (num < _clothesList.Count)
                {
                    var value = _clothesList[num];
                    var clothingData = WorldContentBinding.For(Project).Catalog
                        .Blocks[Terrain.ExtractContents(value)]
                        .GetClothingData(Terrain.ExtractData(value));
                    if (clothingData.PlayerLevelRequired > _componentCharacter.CharacterData.Level)
                    {
                        PublishEffect(ClothingEffectKind.LevelRequired, value);
                        _subsystemPickables.AddPickable(value, 1, _componentBody.Position, null, null);
                        _clothesList.RemoveAt(num);
                        flag = true;
                    }
                    else
                    {
                        num++;
                    }
                }

                if (flag)
                {
                    var slot = (ClothingSlot)enumValue;
                    SetClothes(slot, _clothesList);
                    OnSlotChange((int)slot);
                }
            }
        }

        //计算耐久损耗
        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
            _subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled &&
            _subsystemTime.PeriodicGameTimeEvent(2.0, 0.0) &&
            ((_componentLocomotion.LastWalkOrder.HasValue &&
              _componentLocomotion.LastWalkOrder.Value != Vector2.Zero) ||
             (_componentLocomotion.LastSwimOrder.HasValue &&
              _componentLocomotion.LastSwimOrder.Value != Vector3.Zero) ||
             _componentLocomotion.LastJumpOrder != 0f))
        {
            if (_lastTotalElapsedGameTime.HasValue)
            {
                foreach (var enumValue2 in EnumUtils.GetEnumValues(typeof(ClothingSlot)))
                {
                    var flag2 = false;
                    _clothesList.Clear();
                    _clothesList.AddRange(GetClothes((ClothingSlot)enumValue2));
                    for (var i = 0; i < _clothesList.Count; i++)
                    {
                        var value2 = _clothesList[i];
                        var clothingData2 = WorldContentBinding.For(Project).Catalog
                            .Blocks[Terrain.ExtractContents(value2)]
                            .GetClothingData(Terrain.ExtractData(value2));
                        var num2 = _componentVitalStats.Wetness > 0f
                            ? 10f * clothingData2.Sturdiness
                            : 20f * clothingData2.Sturdiness;
                        var num3 = MathUtils.Floor(_lastTotalElapsedGameTime.Value / num2);
                        if (MathUtils.Floor(_subsystemGameInfo.TotalElapsedGameTime / num2) > num3 &&
                            _random.Float(0f, 1f) < 0.75f)
                        {
                            _clothesList[i] = WorldContentBinding.For(Project).Catalog
                                .DamageItem(value2, 1);
                            flag2 = true;
                        }
                    }

                    var num4 = 0;
                    while (num4 < _clothesList.Count)
                    {
                        if (Terrain.ExtractContents(_clothesList[num4]) != 203)
                        {
                            _clothesList.RemoveAt(num4);
                            PublishEffect(ClothingEffectKind.WornOut);
                        }
                        else
                        {
                            num4++;
                        }
                    }

                    //服务器计算耐久
                    if (!flag2)
                    {
                        continue;
                    }

                    var slot = (ClothingSlot)enumValue2;
                    SetClothes(slot, _clothesList);
                    OnSlotChange(enumValue2);
                }
            }

            _lastTotalElapsedGameTime = _subsystemGameInfo.TotalElapsedGameTime;
        }
    }

    public ReadOnlyList<int> GetClothes(ClothingSlot slot)
    {
        return new ReadOnlyList<int>(_clothes[slot]);
    }

    public virtual void SetClothes(ClothingSlot slot, IEnumerable<int> clothes)
    {
        var clothesArray = clothes as int[] ?? clothes.ToArray();
        if (_clothes[slot].SequenceEqual(clothesArray))
        {
            return;
        }

        _clothes[slot].Clear();
        _clothes[slot].AddRange(clothesArray);
        SanitizeInvalidClothes();
        var num = (from clothe in _clothes
            from item in clothe.Value
            select GetClothingDataSafe(item)
            into clothingData
            where clothingData != null
            select clothingData.DensityModifier).Sum();

        var num2 = num - _densityModifierApplied;
        _densityModifierApplied += num2;
        _componentBody.Density += num2;
        SteedMovementSpeedFactor = 1f;
        var num3 = 2f;
        var num4 = 0.2f;
        var num5 = 0.4f;
        var num6 = 2f;
        foreach (var clothe2 in GetClothes(ClothingSlot.Head))
        {
            var clothingData2 = GetClothingDataSafe(clothe2);
            if (clothingData2 == null)
            {
                continue;
            }

            num3 += clothingData2.Insulation;
            SteedMovementSpeedFactor *= clothingData2.SteedMovementSpeedFactor;
        }

        foreach (var clothe3 in GetClothes(ClothingSlot.Torso))
        {
            var clothingData3 = GetClothingDataSafe(clothe3);
            if (clothingData3 == null)
            {
                continue;
            }

            num4 += clothingData3.Insulation;
            SteedMovementSpeedFactor *= clothingData3.SteedMovementSpeedFactor;
        }

        foreach (var clothe4 in GetClothes(ClothingSlot.Legs))
        {
            var clothingData4 = GetClothingDataSafe(clothe4);
            if (clothingData4 == null)
            {
                continue;
            }

            num5 += clothingData4.Insulation;
            SteedMovementSpeedFactor *= clothingData4.SteedMovementSpeedFactor;
        }

        foreach (var clothe5 in GetClothes(ClothingSlot.Feet))
        {
            var clothingData5 = GetClothingDataSafe(clothe5);
            if (clothingData5 == null)
            {
                continue;
            }

            num6 += clothingData5.Insulation;
            SteedMovementSpeedFactor *= clothingData5.SteedMovementSpeedFactor;
        }

        Insulation = 1f / (1f / num3 + 1f / num4 + 1f / num5 + 1f / num6);
        var num7 = MathUtils.Min(num3, num4, num5, num6);
        if (num3.CloseTo(num7))
        {
            LeastInsulatedSlot = ClothingSlot.Head;
        }
        else if (num4.CloseTo(num7))
        {
            LeastInsulatedSlot = ClothingSlot.Torso;
        }
        else if (num5.CloseTo(num7))
        {
            LeastInsulatedSlot = ClothingSlot.Legs;
        }
        else if (num6.CloseTo(num7))
        {
            LeastInsulatedSlot = ClothingSlot.Feet;
        }

        ClothesChanged?.Invoke();
    }

    public ClothingData? GetClothingDataSafe(int value)
    {
        try
        {
            return WorldContentBinding.For(Project).Catalog.Blocks[Terrain.ExtractContents(value)]
                .GetClothingData(Terrain.ExtractData(value));
        }
        catch (Exception ex)
        {
            Log.Warning($"Ignore invalid clothing value={value}. {ex.Message}");
            return null;
        }
    }

    private void SanitizeInvalidClothes()
    {
        foreach (var pair in _clothes)
        {
            var list = pair.Value;
            var i = 0;
            while (i < list.Count)
            {
                if (GetClothingDataSafe(list[i]) == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                i++;
            }
        }
    }

    public virtual float ApplyArmorProtection(float attackPower)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return attackPower;
        }

        var num = _random.Float(0f, 1f);
        var slot = num < 0.1f ? ClothingSlot.Feet :
            num < 0.3f ? ClothingSlot.Legs :
            num < 0.9f ? ClothingSlot.Torso : ClothingSlot.Head;
        float num2 = ((ClothingBlock)WorldContentBinding.For(Project).Catalog.Blocks[203])
                     .Durability +
                     1;
        var list = new List<int>(GetClothes(slot));
        for (var i = 0; i < list.Count; i++)
        {
            var value = list[i];
            var clothingData = WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(value)]
                .GetClothingData(Terrain.ExtractData(value));
            var x = (num2 - WorldContentBinding.For(Project).Catalog.Blocks[203].GetDamage(value)) /
                    num2 *
                    clothingData.Sturdiness;
            var num3 = MathUtils.Min(attackPower * MathUtils.Saturate(clothingData.ArmorProtection), x);
            if (!(num3 > 0f))
            {
                continue;
            }

            attackPower -= num3;
            if (_subsystemGameInfo.WorldSettings.GameMode != 0)
            {
                var x2 = num3 / clothingData.Sturdiness * num2 + 0.001f;
                var damageCount =
                    (int)(MathUtils.Floor(x2) + (_random.Bool(MathUtils.Remainder(x2, 1f)) ? 1 : 0));
                list[i] = WorldContentBinding.For(Project).Catalog.DamageItem(value, damageCount);
            }

            if (!string.IsNullOrEmpty(clothingData.ImpactSoundsFolder))
            {
                PublishEffect(ClothingEffectKind.ArmorImpact, value);
            }
        }

        var num4 = 0;
        while (num4 < list.Count)
        {
            if (Terrain.ExtractContents(list[num4]) != 203)
            {
                list.RemoveAt(num4);
                PublishEffect(ClothingEffectKind.Broken);
                continue;
            }

            num4++;
        }

        SetClothes(slot, list);
        OnSlotChange((int)slot);

        return MathUtils.Max(attackPower, 0f);
    }

    private void PublishEffect(ClothingEffectKind kind, int value = 0)
    {
        NetworkSender.SendToObservers(
            Entity,
            new ClothingEffectPackage
            {
                EntityId = Entity.EntityId, Kind = kind, ClothingValue = value
            }
        );
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _componentBody = Entity.FindComponent<ComponentBody>(true)!;
        _componentVitalStats = Entity.FindComponent<ComponentVitalStats>(true)!;
        _componentLocomotion = Entity.FindComponent<ComponentLocomotion>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        SteedMovementSpeedFactor = 1f;
        Insulation = 0f;
        LeastInsulatedSlot = ClothingSlot.Feet;
        _clothes[ClothingSlot.Head] = [];
        _clothes[ClothingSlot.Torso] = [];
        _clothes[ClothingSlot.Legs] = [];
        _clothes[ClothingSlot.Feet] = [];
        var value = valuesDictionary.GetValue<ValuesDictionary>("Clothes");
        SetClothes(
            ClothingSlot.Head,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Head"))
        );
        SetClothes(
            ClothingSlot.Torso,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Torso"))
        );
        SetClothes(
            ClothingSlot.Legs,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Legs"))
        );
        SetClothes(
            ClothingSlot.Feet,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Feet"))
        );
        Id = valuesDictionary.GetValue("Id", -1);
        var subInventory = Project.FindSubsystem<SubsystemInventories>(true)!;
        Id = Id == -1 ? subInventory.ProduceInventoryId(this) : subInventory.RegisterInventory(this);
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Clothes", valuesDictionary2);
        valuesDictionary2.SetValue(
            "Head",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Head].ToArray())
        );
        valuesDictionary2.SetValue(
            "Torso",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Torso].ToArray())
        );
        valuesDictionary2.SetValue(
            "Legs",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Legs].ToArray())
        );
        valuesDictionary2.SetValue(
            "Feet",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Feet].ToArray())
        );
        valuesDictionary.SetValue("Id", Id);
    }

    public virtual bool CanWearClothing(int value)
    {
        var clothingData = WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(value)]
            .GetClothingData(Terrain.ExtractData(value));
        var list = GetClothes(clothingData.Slot);
        if (list.Count == 0)
        {
            return true;
        }

        var v2 = list[^1];
        var clothingData2 = WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(v2)]
            .GetClothingData(Terrain.ExtractData(v2));
        return clothingData.Layer > clothingData2.Layer;
    }
}
