using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemBowBlockBehavior : SubsystemBlockBehavior
{
    private const string _typeName = nameof(SubsystemBowBlockBehavior);

    private readonly Dictionary<ComponentMiner, double> _aimStartTimes = new();

    private readonly Random _random = new();

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly ArrowBlock.ArrowType[] _supportedArrowTypes =
    [
        ArrowBlock.ArrowType.WoodenArrow,
        ArrowBlock.ArrowType.StoneArrow,
        ArrowBlock.ArrowType.CopperArrow,
        ArrowBlock.ArrowType.IronArrow,
        ArrowBlock.ArrowType.DiamondArrow,
        ArrowBlock.ArrowType.FireArrow
    ];

    public event Action<WeaponAimRequest>? AimPresentationRequested;

    public event Action<WeaponSoundRequest>? SoundRequested;

    public event Action<WeaponInventoryEditRequest>? InventoryEditRequested;

    public event Action<WeaponFeedbackRequest>? FeedbackRequested;

    public override ResourceId[] HandledBlocks => [];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentCharacter componentCharacter)
    {
        if (componentCharacter.IsLocallyControlled)
        {
            InventoryEditRequested?.Invoke(
                new WeaponInventoryEditRequest(componentCharacter, inventory, slotIndex, WeaponInventoryKind.Bow)
            );
        }

        return true;
    }

    public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
    {
        var inventory = componentMiner.Inventory;
        if (inventory == null)
        {
            return false;
        }

        var activeSlotIndex = inventory.ActiveSlotIndex;
        if (activeSlotIndex < 0)
        {
            return false;
        }

        var slotValue = inventory.GetSlotValue(activeSlotIndex);
        var slotCount = inventory.GetSlotCount(activeSlotIndex);
        var num = Terrain.ExtractContents(slotValue);
        var data = Terrain.ExtractData(slotValue);
        if (num != 191 || slotCount <= 0)
        {
            return false;
        }

        if (!_aimStartTimes.TryGetValue(componentMiner, out var value))
        {
            value = _subsystemTime.GameTime;
            _aimStartTimes[componentMiner] = value;
        }

        var num2 = (float)(_subsystemTime.GameTime - value);
        var num3 = (float)MathUtils.Remainder(_subsystemTime.GameTime, 1000.0);
        var v = ((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.02f : 0.04f) +
                 0.25f * MathUtils.Saturate((num2 - 2.1f) / 5f)) *
                new Vector3
                {
                    X = SimplexNoise.OctavedNoise(num3, 2f, 3, 2f, 0.5f),
                    Y = SimplexNoise.OctavedNoise(num3 + 100f, 2f, 3, 2f, 0.5f),
                    Z = SimplexNoise.OctavedNoise(num3 + 200f, 2f, 3, 2f, 0.5f)
                };
        aim.Direction = Vector3.Normalize(aim.Direction + v);
        switch (state)
        {
            case AimState.InProgress:
            {
                if (num2 >= 9f)
                {
                    componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
                    return true;
                }

                AimPresentationRequested?.Invoke(
                    new WeaponAimRequest(componentMiner, aim, new Vector3(-0.1f, 0.15f, 0f), new Vector3(0f, -0.7f, 0f))
                );

                componentMiner.ComponentCreature.ComponentCreaturePose.AimHandAngleOrder = 1.2f;
                componentMiner.ComponentCreature.ComponentCreaturePose.InHandItemOffsetOrder =
                    new Vector3(0f, 0f, 0f);
                componentMiner.ComponentCreature.ComponentCreaturePose.InHandItemRotationOrder =
                    new Vector3(0f, -0.2f, 0f);
                if (_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
                {
                    if (WorldRuntimeContext.For(Project).IsAuthority)
                    {
                        var draw2 = MathUtils.Min(BowBlock.GetDraw(data) + 1, 15);
                        inventory.RemoveSlotItems(activeSlotIndex, 1);
                        inventory.AddSlotItems(
                            activeSlotIndex,
                            Terrain.MakeBlockValue(num, 0, BowBlock.SetDraw(data, draw2)),
                            1
                        );
                    }
                }

                break;
            }
            case AimState.Cancelled:
                if (WorldRuntimeContext.For(Project).IsAuthority)
                {
                    inventory.RemoveSlotItems(activeSlotIndex, 1);
                    inventory.AddSlotItems(
                        activeSlotIndex,
                        Terrain.MakeBlockValue(num, 0, BowBlock.SetDraw(data, 0)),
                        1
                    );
                }

                _aimStartTimes.Remove(componentMiner);
                break;
            case AimState.Completed:
            {
                var draw = BowBlock.GetDraw(data);
                var arrowType = BowBlock.GetArrowType(data);
                if (arrowType.HasValue)
                {
                    var vector = componentMiner.ComponentCreature.ComponentCreaturePose.EyePosition +
                                 componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f -
                                 componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
                    var vector2 = Vector3.Normalize(vector + aim.Direction * 10f - vector);
                    var num4 = MathUtils.Lerp(0f, 28f, MathUtils.Pow(draw / 15f, 0.75f));
                    if (componentMiner.ComponentCharacter != null)
                    {
                        num4 *= 0.5f * (componentMiner.ComponentCharacter.ComponentLevel.StrengthFactor - 1f) +
                                1f;
                    }

                    var vector3 = Vector3.Zero;
                    if (arrowType == ArrowBlock.ArrowType.WoodenArrow)
                    {
                        vector3 = new Vector3(0.025f, 0.025f, 0.025f);
                    }

                    if (arrowType == ArrowBlock.ArrowType.StoneArrow)
                    {
                        vector3 = new Vector3(0.01f, 0.01f, 0.01f);
                    }

                    var value2 = Terrain.MakeBlockValue(
                        192,
                        0,
                        ArrowBlock.SetArrowType(0, arrowType.Value)
                    );
                    var vector4 = Vector3.Normalize(Vector3.Cross(vector2, Vector3.UnitY));
                    var v2 = Vector3.Normalize(Vector3.Cross(vector2, vector4));
                    var v3 = _random.Float(0f - vector3.X, vector3.X) * vector4 +
                             _random.Float(0f - vector3.Y, vector3.Y) * v2 +
                             _random.Float(0f - vector3.Z, vector3.Z) * vector2;
                    if (_subsystemProjectiles.FireProjectile(
                            value2,
                            vector,
                            (vector2 + v3) * num4,
                            Vector3.Zero,
                            componentMiner.ComponentCreature
                        ) !=
                        null)
                    {
                        data = BowBlock.SetArrowType(data, null);
                        RequestSound(
                            "Audio/Bow",
                            1f,
                            _random.Float(-0.1f, 0.1f),
                            vector,
                            3f,
                            true
                        );
                    }
                }
                else
                {
                    RequestFeedback(componentMiner, 0);
                }

                if (WorldRuntimeContext.For(Project).IsAuthority)
                {
                    inventory.RemoveSlotItems(activeSlotIndex, 1);
                    var value3 = Terrain.MakeBlockValue(num, 0, BowBlock.SetDraw(data, 0));
                    inventory.AddSlotItems(activeSlotIndex, value3, 1);
                    var damageCount = 0;
                    if (draw >= 15)
                    {
                        damageCount = 2;
                    }
                    else if (draw >= 4)
                    {
                        damageCount = 1;
                    }

                    componentMiner.DamageActiveTool(damageCount);
                }

                _aimStartTimes.Remove(componentMiner);
                return true;
            }
        }

        return false;
    }

    public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
    {
        var num = Terrain.ExtractContents(value);
        var arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(value));
        if (num == 192 && _supportedArrowTypes.Contains(arrowType))
        {
            return !BowBlock.GetArrowType(Terrain.ExtractData(inventory.GetSlotValue(slotIndex))).HasValue ? 1 : 0;
        }

        return 0;
    }

    public override void ProcessInventoryItem(
        IInventory inventory,
        int slotIndex,
        int value,
        int count,
        int processCount,
        out int processedValue,
        out int processedCount
    )
    {
        if (processCount == 1)
        {
            var arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(value));
            var data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
            processedValue = 0;
            processedCount = 0;
            inventory.RemoveSlotItems(slotIndex, 1);
            inventory.AddSlotItems(
                slotIndex,
                Terrain.MakeBlockValue(191, 0, BowBlock.SetArrowType(data, arrowType)),
                1
            );
        }
        else
        {
            processedValue = value;
            processedCount = count;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        base.Load(valuesDictionary);
    }

    private void RequestSound(
        string name,
        float volume,
        float pitch,
        Vector3 position,
        float minDistance,
        bool autoDelay
    )
    {
        var request = new WeaponSoundRequest(name, volume, pitch, position, minDistance, autoDelay ? null : 0f);
        SoundRequested?.Invoke(request);
    }

    private void RequestFeedback(ComponentMiner miner, int key)
    {
        if (miner.ComponentCharacter is { IsLocallyControlled: true } character)
        {
            FeedbackRequested?.Invoke(
                new WeaponFeedbackRequest(
                    character,
                    new LocalizedText(
                        _typeName,
                        key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        string.Empty
                    )
                )
            );
        }
    }

    public override void Dispose()
    {
        AimPresentationRequested = null;
        SoundRequested = null;
        InventoryEditRequested = null;
        FeedbackRequested = null;
        base.Dispose();
    }
}
