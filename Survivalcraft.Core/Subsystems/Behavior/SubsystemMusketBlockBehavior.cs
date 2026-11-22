using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemMusketBlockBehavior : SubsystemBlockBehavior
{
    private const string _typeName = nameof(SubsystemMusketBlockBehavior);

    private readonly Dictionary<ComponentMiner, double> _aimStartTimes = new();

    private readonly Random _random = new();

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemTime _subsystemTime = null!;

    public event Action<WeaponAimRequest>? AimPresentationRequested;

    public event Action<WeaponSoundRequest>? SoundRequested;

    public event Action<WeaponInventoryEditRequest>? InventoryEditRequested;

    public event Action<WeaponFeedbackRequest>? FeedbackRequested;

    public event Action<WeaponSmokeRequest>? SmokeRequested;

    public override ResourceId[] HandledBlocks => [];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentCharacter componentCharacter)
    {
        if (componentCharacter.IsLocallyControlled)
        {
            InventoryEditRequested?.Invoke(
                new WeaponInventoryEditRequest(componentCharacter, inventory, slotIndex, WeaponInventoryKind.Musket)
            );
        }

        return true;
    }

    public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
    {
        var inventory = componentMiner.Inventory;
        var activeSlotIndex = inventory.ActiveSlotIndex;
        if (activeSlotIndex < 0)
        {
            return false;
        }

        var slotValue = inventory.GetSlotValue(activeSlotIndex);
        var slotCount = inventory.GetSlotCount(activeSlotIndex);
        var heldContents = Terrain.ExtractContents(slotValue);
        var data = Terrain.ExtractData(slotValue);
        var updatedValue = slotValue;
        var toolDamage = 0;
        if (heldContents != 212 || slotCount <= 0)
        {
            return false;
        }

        if (!_aimStartTimes.TryGetValue(componentMiner, out var value))
        {
            value = _subsystemTime.GameTime;
            _aimStartTimes[componentMiner] = value;
        }

        var aimDuration = (float)(_subsystemTime.GameTime - value);
        var swayTime = (float)MathUtils.Remainder(_subsystemTime.GameTime, 1000.0);
        var v = ((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.01f : 0.03f) +
                 0.2f * MathUtils.Saturate((aimDuration - 2.5f) / 6f)) *
                new Vector3
                {
                    X = SimplexNoise.OctavedNoise(swayTime, 2f, 3, 2f, 0.5f),
                    Y = SimplexNoise.OctavedNoise(swayTime + 100f, 2f, 3, 2f, 0.5f),
                    Z = SimplexNoise.OctavedNoise(swayTime + 200f, 2f, 3, 2f, 0.5f)
                };
        aim.Direction = Vector3.Normalize(aim.Direction + v);
        switch (state)
        {
            case AimState.InProgress:
            {
                if (aimDuration >= 10f)
                {
                    componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
                    return true;
                }

                if (aimDuration > 0.5f && !MusketBlock.GetHammerState(Terrain.ExtractData(updatedValue)))
                {
                    updatedValue = Terrain.MakeBlockValue(
                        heldContents,
                        MusketBlock.SetHammerState(Terrain.ExtractData(updatedValue), true)
                    );
                    if (componentMiner.ComponentCharacter != null)
                    {
                        RequestSound(
                            "Audio/HammerCock",
                            1f,
                            _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCharacter.ComponentCreaturePose.EyePosition,
                            64f,
                            false
                        );
                    }
                }

                AimPresentationRequested?.Invoke(
                    new WeaponAimRequest(
                        componentMiner,
                        aim,
                        new Vector3(-0.21f, 0.15f, 0.08f),
                        new Vector3(-0.7f, 0f, 0f)
                    )
                );

                componentMiner.ComponentCreature.ComponentCreaturePose.AimHandAngleOrder = 1.4f;
                componentMiner.ComponentCreature.ComponentCreaturePose.InHandItemOffsetOrder =
                    new Vector3(-0.08f, -0.08f, 0.07f);
                componentMiner.ComponentCreature.ComponentCreaturePose.InHandItemRotationOrder =
                    new Vector3(-1.7f, 0f, 0f);
                break;
            }
            case AimState.Cancelled:
                if (MusketBlock.GetHammerState(Terrain.ExtractData(updatedValue)))
                {
                    updatedValue = Terrain.MakeBlockValue(
                        heldContents,
                        MusketBlock.SetHammerState(Terrain.ExtractData(updatedValue), false)
                    );
                    if (componentMiner.ComponentCharacter != null)
                    {
                        RequestSound(
                            "Audio/HammerUncock",
                            1f,
                            _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCharacter.ComponentCreaturePose.EyePosition,
                            64f,
                            false
                        );
                    }
                }

                _aimStartTimes.Remove(componentMiner);
                break;
            case AimState.Completed:
            {
                var shouldFire = false;
                var value2 = 0;
                var projectileCount = 0;
                var s = 0f;
                var vector = Vector3.Zero;
                var loadState = MusketBlock.GetLoadState(data);
                var bulletType = MusketBlock.GetBulletType(data);
                if (MusketBlock.GetHammerState(Terrain.ExtractData(updatedValue)))
                {
                    switch (loadState)
                    {
                        case MusketBlock.LoadState.Empty:
                            RequestFeedback(componentMiner, 0);
                            break;
                        case MusketBlock.LoadState.Gunpowder:
                        case MusketBlock.LoadState.Wad:
                            shouldFire = true;
                            RequestFeedback(componentMiner, 1);
                            break;
                        case MusketBlock.LoadState.Loaded:
                            shouldFire = true;
                            if (bulletType == BulletBlock.BulletType.Buckshot)
                            {
                                value2 = Terrain.MakeBlockValue(
                                    214,
                                    BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall)
                                );
                                projectileCount = 8;
                                vector = new Vector3(0.04f, 0.04f, 0.25f);
                                s = 80f;
                            }
                            else if (bulletType == BulletBlock.BulletType.BuckshotBall)
                            {
                                value2 = Terrain.MakeBlockValue(
                                    214,
                                    BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall)
                                );
                                projectileCount = 1;
                                vector = new Vector3(0.06f, 0.06f, 0f);
                                s = 60f;
                            }
                            else if (bulletType.HasValue)
                            {
                                value2 = Terrain.MakeBlockValue(
                                    214,
                                    BulletBlock.SetBulletType(0, bulletType.Value)
                                );
                                projectileCount = 1;
                                s = 120f;
                            }

                            break;
                    }
                }

                if (shouldFire)
                {
                    if (componentMiner.ComponentCreature.ComponentBody.ImmersionFactor > 0.4f)
                    {
                        RequestSound(
                            "Audio/MusketMisfire",
                            1f,
                            _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCreature.ComponentCreaturePose.EyePosition,
                            8f,
                            true
                        );
                    }
                    else
                    {
                        var muzzlePosition = componentMiner.ComponentCreature.ComponentCreaturePose.EyePosition +
                                             componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f -
                                             componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
                        var shotDirection = Vector3.Normalize(muzzlePosition + aim.Direction * 10f - muzzlePosition);
                        var shotRight = Vector3.Normalize(Vector3.Cross(shotDirection, Vector3.UnitY));
                        var v2 = Vector3.Normalize(Vector3.Cross(shotDirection, shotRight));
                        for (var i = 0; i < projectileCount; i++)
                        {
                            var v3 = _random.Float(0f - vector.X, vector.X) * shotRight +
                                     _random.Float(0f - vector.Y, vector.Y) * v2 +
                                     _random.Float(0f - vector.Z, vector.Z) * shotDirection;
                            var projectile = _subsystemProjectiles.FireProjectile(
                                value2,
                                muzzlePosition,
                                s * (shotDirection + v3),
                                Vector3.Zero,
                                componentMiner.ComponentCreature
                            );
                            if (projectile != null)
                            {
                                projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
                            }
                        }

                        RequestSound(
                            "Audio/MusketFire",
                            1f,
                            _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCreature.ComponentCreaturePose.EyePosition,
                            64f,
                            true
                        );
                        SmokeRequested?.Invoke(
                            new WeaponSmokeRequest(muzzlePosition + 0.3f * shotDirection, shotDirection)
                        );
                        _subsystemNoise.MakeNoise(muzzlePosition, 1f, 40f);
                        componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * shotDirection);
                    }

                    updatedValue = Terrain.MakeBlockValue(
                        Terrain.ExtractContents(updatedValue),
                        MusketBlock.SetLoadState(Terrain.ExtractData(updatedValue), MusketBlock.LoadState.Empty)
                    );
                    toolDamage = 1;
                }

                if (MusketBlock.GetHammerState(Terrain.ExtractData(updatedValue)))
                {
                    updatedValue = Terrain.MakeBlockValue(
                        Terrain.ExtractContents(updatedValue),
                        MusketBlock.SetHammerState(Terrain.ExtractData(updatedValue), false)
                    );
                    RequestSound(
                        "Audio/HammerRelease",
                        1f,
                        _random.Float(-0.1f, 0.1f),
                        componentMiner.ComponentCreature.ComponentCreaturePose.EyePosition,
                        64f,
                        false
                    );
                }

                _aimStartTimes.Remove(componentMiner);
                if (!WorldRuntimeContext.For(Project).IsAuthority)
                {
                    return true;
                }

                if (updatedValue != slotValue)
                {
                    inventory.RemoveSlotItems(activeSlotIndex, 1);
                    inventory.AddSlotItems(activeSlotIndex, updatedValue, 1);
                }

                if (toolDamage > 0)
                {
                    componentMiner.DamageActiveTool(toolDamage);
                }

                return true;
            }
        }

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            if (updatedValue != slotValue)
            {
                inventory.RemoveSlotItems(activeSlotIndex, 1);
                inventory.AddSlotItems(activeSlotIndex, updatedValue, 1);
            }

            if (toolDamage > 0)
            {
                componentMiner.DamageActiveTool(toolDamage);
            }
        }
        else
        {
            //防止客户端声音重复
            if (updatedValue == slotValue)
            {
                return false;
            }

            inventory.RemoveNetSlotItems(activeSlotIndex, 1);
            inventory.AddNetSlotItems(activeSlotIndex, updatedValue, 1);
        }

        return false;
    }

    public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
    {
        var itemContents = Terrain.ExtractContents(value);
        var loadState = MusketBlock.GetLoadState(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
        if (loadState == MusketBlock.LoadState.Empty && itemContents == 109)
        {
            return 1;
        }

        if (loadState == MusketBlock.LoadState.Gunpowder && itemContents == 205)
        {
            return 1;
        }

        if (loadState == MusketBlock.LoadState.Wad && itemContents == 214)
        {
            return 1;
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
        processedValue = value;
        processedCount = count;
        if (processCount == 1)
        {
            var data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
            var loadState = MusketBlock.GetLoadState(data);
            var bulletType = MusketBlock.GetBulletType(data);
            switch (loadState)
            {
                case MusketBlock.LoadState.Empty:
                    loadState = MusketBlock.LoadState.Gunpowder;
                    bulletType = null;
                    break;
                case MusketBlock.LoadState.Gunpowder:
                    loadState = MusketBlock.LoadState.Wad;
                    bulletType = null;
                    break;
                case MusketBlock.LoadState.Wad:
                {
                    loadState = MusketBlock.LoadState.Loaded;
                    var data2 = Terrain.ExtractData(value);
                    bulletType = BulletBlock.GetBulletType(data2);
                    break;
                }
            }

            processedValue = 0;
            processedCount = 0;
            inventory.RemoveSlotItems(slotIndex, 1);
            inventory.AddSlotItems(
                slotIndex,
                Terrain.MakeBlockValue(
                    212,
                    MusketBlock.SetBulletType(MusketBlock.SetLoadState(data, loadState), bulletType)
                ),
                1
            );
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
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
        SmokeRequested = null;
        base.Dispose();
    }
}
