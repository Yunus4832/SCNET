using Engine.Graphics;
using Engine.Serialization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentClothing : Component, IUpdateable, IInventory
{
    private const string _typeName = "ComponentClothing";

    private static readonly ClothingSlot[] _innerSlotsOrder =
    [
        ClothingSlot.Head,
        ClothingSlot.Torso,
        ClothingSlot.Feet,
        ClothingSlot.Legs
    ];

    private static readonly ClothingSlot[] _outerSlotsOrder =
    [
        ClothingSlot.Head,
        ClothingSlot.Torso,
        ClothingSlot.Legs,
        ClothingSlot.Feet
    ];

    public static bool ShowClothedTexture = false;

    private static bool _drawClothedTexture = true;

    private bool _clothedTexturesValid;

    private readonly Dictionary<ClothingSlot, List<int>> _clothes = new();

    private readonly List<int> _clothesList = [];

    private ComponentBody _componentBody = null!;

    private ComponentGui _componentGui = null!;

    private ComponentHumanModel _componentHumanModel = null!;

    private ComponentLocomotion _componentLocomotion = null!;

    private ComponentOuterClothingModel _componentOuterClothingModel = null!;

    private ComponentPlayer _componentPlayer = null!;

    private ComponentVitalStats _componentVitalStats = null!;

    private float _densityModifierApplied;

    private RenderTarget2D? _innerClothedTexture;

    private double? _lastTotalElapsedGameTime;

    private RenderTarget2D? _outerClothedTexture;

    private PrimitivesRenderer2D _primitivesRenderer = new();

    private readonly Random _random = new();

    private Texture2D? _skinTexture;

    private string _skinTextureName = string.Empty;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public Texture2D InnerClothedTexture => _innerClothedTexture ?? throw new InvalidOperationException("InnerClothTexture is not initialized");

    public Texture2D OuterClothedTexture => _outerClothedTexture ?? throw new InvalidOperationException("OuterClothTexture is not initialized");

    public float Insulation { get; set; }

    public ClothingSlot LeastInsulatedSlot { get; set; }

    public float SteedMovementSpeedFactor { get; set; }

    public int Id { get; private set; }

    Project IInventory.Project => Project;

    public int SlotsCount => 4;

    public int VisibleSlotsCount
    {
        get => SlotsCount;
        set { }
    }

    public int ActiveSlotIndex
    {
        get => -1;
        set { }
    }

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
        var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
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

    public virtual void ProcessSlotItems(IInventory sourceInventory, int sourceSlotIndex, int slotIndex, int value,
        int count, int processCount, out int processedValue, out int processedCount)
    {
        processedCount = 0;
        processedValue = 0;
        if (processCount != 1)
        {
            return;
        }

        var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
        ModsManager.HookAction("ClothingProcessSlotItems",
            modLoader =>
            {
                return modLoader.ClothingProcessSlotItems(_componentPlayer, block, slotIndex, value, count);
            });
        if (block.GetNutritionalValue(value) > 0f)
        {
            if (block is BucketBlock)
            {
                processedValue = Terrain.MakeBlockValue(90, 0, Terrain.ExtractData(value));
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
            var clothingData = BlocksManager.Blocks[Terrain.ExtractContents(value)]
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
        var random = new Random();
        var subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        for (var i = 0; i < SlotsCount; i++)
        {
            var x = GetClothes((ClothingSlot)i).Count;
            for (var j = 0; j < x; j++)
            {
                var slotValue = GetSlotValue(i);
                var count = RemoveSlotItems(i, 1);
                var value = random.Float(5f, 10f) * Vector3.Normalize(new Vector3(random.Float(-1f, 1f),
                    random.Float(1f, 2f), random.Float(-1f, 1f)));
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
                    var clothingData = BlocksManager.Blocks[Terrain.ExtractContents(value)]
                        .GetClothingData(Terrain.ExtractData(value));
                    if (clothingData.PlayerLevelRequired > _componentPlayer.PlayerData.Level)
                    {
                        _componentGui.DisplaySmallMessage(
                            string.Format(LanguageControl.Get(_typeName, 1), clothingData.PlayerLevelRequired,
                                clothingData.DisplayName), Color.White, true, true);
                        _subsystemPickables.AddPickable(value, 1, _componentBody.Position, null, null);
                        _clothesList.RemoveAt(num);
                        flag = true;
                    }
                    else
                    {
                        num++;
                    }
                }

                if (flag && CommonLib.WorkType != WorkType.Client)
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
              _componentLocomotion.LastSwimOrder.Value != Vector3.Zero) || _componentLocomotion.LastJumpOrder != 0f))
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
                        var clothingData2 = BlocksManager.Blocks[Terrain.ExtractContents(value2)]
                            .GetClothingData(Terrain.ExtractData(value2));
                        var num2 = _componentVitalStats.Wetness > 0f
                            ? 10f * clothingData2.Sturdiness
                            : 20f * clothingData2.Sturdiness;
                        var num3 = MathUtils.Floor(_lastTotalElapsedGameTime.Value / num2);
                        if (MathUtils.Floor(_subsystemGameInfo.TotalElapsedGameTime / num2) > num3 &&
                            _random.Float(0f, 1f) < 0.75f)
                        {
                            _clothesList[i] = BlocksManager.DamageItem(value2, 1);
                            flag2 = true;
                        }
                    }

                    var num4 = 0;
                    while (num4 < _clothesList.Count)
                    {
                        if (Terrain.ExtractContents(_clothesList[num4]) != 203)
                        {
                            _clothesList.RemoveAt(num4);
                            _subsystemParticles.AddParticleSystem(new BlockDebrisParticleSystem(_subsystemTerrain,
                                _componentBody.Position + _componentBody.StanceBoxSize / 2f, 1f, 1f, Color.White, 0));
                            _componentGui.DisplaySmallMessage(LanguageControl.Get(_typeName, 2), Color.White, true,
                                true);
                        }
                        else
                        {
                            num4++;
                        }
                    }

                    //服务器计算耐久
                    if (flag2 && CommonLib.WorkType != WorkType.Client)
                    {
                        var slot = (ClothingSlot)enumValue2;
                        SetClothes(slot, _clothesList);
                        OnSlotChange(enumValue2);
                    }
                }
            }

            _lastTotalElapsedGameTime = _subsystemGameInfo.TotalElapsedGameTime;
        }

#if !SERVER
        UpdateRenderTargets();
#endif
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
        _clothedTexturesValid = false;
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
    }

    private ClothingData? GetClothingDataSafe(int value)
    {
        try
        {
            return BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(Terrain.ExtractData(value));
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
        var applied = false;
        ModsManager.HookAction("ApplyArmorProtection", modLoader =>
        {
            attackPower = modLoader.ApplyArmorProtection(this, attackPower, out var flag2);
            applied |= flag2;
            return false;
        });
        if (applied)
        {
            return MathUtils.Max(attackPower, 0f);
        }

        var num = _random.Float(0f, 1f);
        var slot = num < 0.1f ? ClothingSlot.Feet :
            num < 0.3f ? ClothingSlot.Legs :
            num < 0.9f ? ClothingSlot.Torso : ClothingSlot.Head;
        float num2 = ((ClothingBlock)BlocksManager.Blocks[203]).Durability + 1;
        var list = new List<int>(GetClothes(slot));
        for (var i = 0; i < list.Count; i++)
        {
            var value = list[i];
            var clothingData = BlocksManager.Blocks[Terrain.ExtractContents(value)]
                .GetClothingData(Terrain.ExtractData(value));
            var x = (num2 - BlocksManager.Blocks[203].GetDamage(value)) / num2 * clothingData.Sturdiness;
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
                list[i] = BlocksManager.DamageItem(value, damageCount);
            }

            if (!string.IsNullOrEmpty(clothingData.ImpactSoundsFolder))
            {
                _subsystemAudio.PlayRandomSound(clothingData.ImpactSoundsFolder, 1f,
                    _random.Float(-0.3f, 0.3f), _componentBody.Position, 4f, 0.15f);
            }
        }

        var num4 = 0;
        while (num4 < list.Count)
        {
            if (Terrain.ExtractContents(list[num4]) != 203)
            {
                list.RemoveAt(num4);
                _subsystemParticles.AddParticleSystem(new BlockDebrisParticleSystem(_subsystemTerrain,
                    _componentBody.Position + _componentBody.StanceBoxSize / 2f, 1f, 1f, Color.White, 0));
                continue;
            }

            num4++;
        }

        SetClothes(slot, list);
        OnSlotChange((int)slot);

        return MathUtils.Max(attackPower, 0f);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _componentGui = Entity.FindComponent<ComponentGui>(true)!;
        _componentHumanModel = Entity.FindComponent<ComponentHumanModel>(true)!;
        _componentBody = Entity.FindComponent<ComponentBody>(true)!;
        _componentOuterClothingModel = Entity.FindComponent<ComponentOuterClothingModel>(true)!;
        _componentVitalStats = Entity.FindComponent<ComponentVitalStats>(true)!;
        _componentLocomotion = Entity.FindComponent<ComponentLocomotion>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        SteedMovementSpeedFactor = 1f;
        Insulation = 0f;
        LeastInsulatedSlot = ClothingSlot.Feet;
        _clothes[ClothingSlot.Head] = [];
        _clothes[ClothingSlot.Torso] = [];
        _clothes[ClothingSlot.Legs] = [];
        _clothes[ClothingSlot.Feet] = [];
        var value = valuesDictionary.GetValue<ValuesDictionary>("Clothes");
        SetClothes(ClothingSlot.Head,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Head")));
        SetClothes(ClothingSlot.Torso,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Torso")));
        SetClothes(ClothingSlot.Legs,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Legs")));
        SetClothes(ClothingSlot.Feet,
            HumanReadableConverter.ValuesListFromString<int>(';', value.GetValue<string>("Feet")));
        Id = valuesDictionary.GetValue("Id", -1);
        var subInventory = Project.FindSubsystem<SubsystemInventories>(true)!;
        Id = Id == -1 ? subInventory.ProduceInventoryId(this) : subInventory.RegisterInventory(this);
        Display.DeviceReset += DisplayDeviceReset;
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Clothes", valuesDictionary2);
        valuesDictionary2.SetValue("Head",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Head].ToArray()));
        valuesDictionary2.SetValue("Torso",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Torso].ToArray()));
        valuesDictionary2.SetValue("Legs",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Legs].ToArray()));
        valuesDictionary2.SetValue("Feet",
            HumanReadableConverter.ValuesListToString(';', _clothes[ClothingSlot.Feet].ToArray()));
        valuesDictionary.SetValue("Id", Id);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_skinTexture != null && !ContentManager.IsContent(_skinTexture))
        {
            _skinTexture.Dispose();
            _skinTexture = null;
        }

        if (_innerClothedTexture != null)
        {
            _innerClothedTexture.Dispose();
            _innerClothedTexture = null;
        }

        if (_outerClothedTexture != null)
        {
            _outerClothedTexture.Dispose();
            _outerClothedTexture = null;
        }

        Display.DeviceReset -= DisplayDeviceReset;
    }

    public virtual void DisplayDeviceReset()
    {
        _clothedTexturesValid = false;
    }

    public virtual bool CanWearClothing(int value)
    {
        var clothingData = BlocksManager.Blocks[Terrain.ExtractContents(value)]
            .GetClothingData(Terrain.ExtractData(value));
        var list = GetClothes(clothingData.Slot);
        if (list.Count == 0)
        {
            return true;
        }

        var v2 = list[^1];
        var clothingData2 = BlocksManager.Blocks[Terrain.ExtractContents(v2)].GetClothingData(Terrain.ExtractData(v2));
        return clothingData.Layer > clothingData2.Layer;
    }

    public virtual void UpdateRenderTargets()
    {
        if (_skinTexture == null || _componentPlayer.PlayerData.CharacterSkinName != _skinTextureName)
        {
            if (CharacterSkinsManager.HasSkinRes(_componentPlayer.PlayerData.CharacterSkinName))
            {
                _skinTexture = CharacterSkinsManager.LoadTexture(_componentPlayer.PlayerData.CharacterSkinName);
                _skinTextureName = _componentPlayer.PlayerData.CharacterSkinName;
                Utilities.Dispose(ref _innerClothedTexture);
                Utilities.Dispose(ref _outerClothedTexture);
            }
            else
            {
                if (Time.PeriodicEvent(1.0, 0.0))
                {
                    if (CommonLib.WorkType == WorkType.Client)
                    {
                        CommonLib.Net.QueuePackage(new ComponentClothingPackage(
                            _componentPlayer.PlayerData.CharacterSkinName,
                            ComponentClothingPackage.DataType.RequestSkin));
                    }
                    else if (CommonLib.WorkType == WorkType.Server)
                    {
                        CommonLib.Net.QueuePackage(new ComponentClothingPackage(
                            _componentPlayer.PlayerData.CharacterSkinName, ComponentClothingPackage.DataType.WhoHas));
                    }
                }
            }

            if (_skinTexture == null)
            {
                _skinTexture = CharacterSkinsManager.LoadTexture("$Male1")!;
            }
        }

        if (_innerClothedTexture == null || _innerClothedTexture.Width != _skinTexture.Width ||
            _innerClothedTexture.Height != _skinTexture.Height)
        {
            _innerClothedTexture = new RenderTarget2D(_skinTexture.Width, _skinTexture.Height, 1,
                ColorFormat.Rgba8888, DepthFormat.None);
            _componentHumanModel.TextureOverride = _innerClothedTexture;
            _clothedTexturesValid = false;
        }

        if (_outerClothedTexture == null || _outerClothedTexture.Width != _skinTexture.Width ||
            _outerClothedTexture.Height != _skinTexture.Height)
        {
            _outerClothedTexture = new RenderTarget2D(_skinTexture.Width, _skinTexture.Height, 1,
                ColorFormat.Rgba8888, DepthFormat.None);
            _componentOuterClothingModel.TextureOverride = _outerClothedTexture;
            _clothedTexturesValid = false;
        }

        if (_drawClothedTexture && !_clothedTexturesValid)
        {
            _clothedTexturesValid = true;
            var scissorRectangle = Display.ScissorRectangle;
            var renderTarget = Display.RenderTarget;
            try
            {
                Display.RenderTarget = _innerClothedTexture;
                Display.Clear(new Vector4(Color.Transparent));
                var num = 0;
                var texturedBatch2D = _primitivesRenderer.TexturedBatch(_skinTexture, false, num++,
                    DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
                texturedBatch2D.QueueQuad(Vector2.Zero,
                    new Vector2(_innerClothedTexture.Width, _innerClothedTexture.Height), 0f, Vector2.Zero,
                    Vector2.One, Color.White);
                var innerSlotsOrder = _innerSlotsOrder;
                foreach (var slot in innerSlotsOrder)
                foreach (var cloth in GetClothes(slot))
                {
                    var data = Terrain.ExtractData(cloth);
                    var clothingData = BlocksManager.Blocks[Terrain.ExtractContents(cloth)].GetClothingData(data);
                    var fabricColor =
                        SubsystemPalette.GetFabricColor(_subsystemTerrain, ClothingBlock.GetClothingColor(data));
                    texturedBatch2D = _primitivesRenderer.TexturedBatch(clothingData.Texture, false, num++,
                        DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
                    if (!clothingData.IsOuter)
                    {
                        texturedBatch2D.QueueQuad(new Vector2(0f, 0f),
                            new Vector2(_innerClothedTexture.Width, _innerClothedTexture.Height), 0f, Vector2.Zero,
                            Vector2.One, fabricColor);
                    }
                }

                _primitivesRenderer.Flush();
                Display.RenderTarget = _outerClothedTexture;
                Display.Clear(new Vector4(Color.Transparent));
                num = 0;
                innerSlotsOrder = _outerSlotsOrder;
                foreach (var slot2 in innerSlotsOrder)
                foreach (var clothe2 in GetClothes(slot2))
                {
                    var data2 = Terrain.ExtractData(clothe2);
                    var clothingData2 = BlocksManager.Blocks[Terrain.ExtractContents(clothe2)].GetClothingData(data2);
                    var fabricColor2 =
                        SubsystemPalette.GetFabricColor(_subsystemTerrain, ClothingBlock.GetClothingColor(data2));
                    texturedBatch2D = _primitivesRenderer.TexturedBatch(clothingData2.Texture, false, num++,
                        DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
                    if (clothingData2.IsOuter)
                    {
                        texturedBatch2D.QueueQuad(new Vector2(0f, 0f),
                            new Vector2(_outerClothedTexture.Width, _outerClothedTexture.Height), 0f, Vector2.Zero,
                            Vector2.One, fabricColor2);
                    }
                }

                _primitivesRenderer.Flush();
            }
            finally
            {
                Display.RenderTarget = renderTarget;
                Display.ScissorRectangle = scissorRectangle;
            }
        }
    }
}
