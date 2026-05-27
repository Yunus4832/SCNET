using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentMiner : Component, IUpdateable
{
    private static readonly Random _random2 = new();

    public const string Name = "ComponentMiner";

    //简单防止内存修改
    private readonly SafeFloat _attackPower = new();

    public bool DigFaceChange;

    private ComponentHealth _componentHealth = null!;

    private float _digProgress;

    private double _digStartTime;

    private int _lastDigFrameIndex;

    private double _lastHitTime;

    private float _lastPokingPhase;

    private double _lastToolHintTime;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemMovingBlocks _subsystemMovingBlocks = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public ComponentCreature ComponentCreature { get; private set; } = null!;

    public ComponentPlayer? ComponentPlayer { get; private set; }

    public float AutoInteractRate { get; private set; }

    public IInventory Inventory
    {
        get;
        private set;
    } = InventoryDefault.Default;

    public int ActiveBlockValue => Inventory.GetSlotValue(Inventory.ActiveSlotIndex);

    private float AttackPower
    {
        get => _attackPower.Get();
        set => _attackPower.Set(value);
    }

    public float PokingPhase { get; set; }

    public CellFace? DigCellFace { get; set; }

    public float DigTime
    {
        get
        {
            if (!DigCellFace.HasValue)
            {
                return 0f;
            }

            return (float)(_subsystemTime.GameTime - _digStartTime);
        }
    }

    public float DigProgress
    {
        get => !DigCellFace.HasValue ? 0f : _digProgress;
        set => _digProgress = value;
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var num = _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative
            ? 0.5f / SettingsManager.CreativeDigTime
            : 4f;
        _lastPokingPhase = PokingPhase;
        if (DigCellFace.HasValue || PokingPhase > 0f)
        {
            PokingPhase += num * _subsystemTime.GameTimeDelta;
            if (PokingPhase > 1f)
            {
                PokingPhase = DigCellFace.HasValue ? MathUtils.Remainder(PokingPhase, 1f) : 0f;
            }
        }

        if (DigCellFace.HasValue && Time.FrameIndex - _lastDigFrameIndex > 1)
        {
            DigCellFace = null;
        }

        if (!(_componentHealth.Health > 0f) || !(AutoInteractRate > 0f) ||
            !_random.Bool(AutoInteractRate) ||
            !_subsystemTime.PeriodicGameTimeEvent(1.0, GetHashCode() % 100 / 100f))
        {
            return;
        }

        var componentCreatureModel = ComponentCreature.ComponentCreatureModel;
        var eyePosition = componentCreatureModel.EyePosition;
        var forwardVector = componentCreatureModel.EyeRotation.GetForwardVector();
        for (var i = 0; i < 10; i++)
        {
            var terrainRaycastResult =
                Raycast<TerrainRaycastResult>(new Ray3(eyePosition, forwardVector + _random.Vector3(0.75f)),
                    RaycastMode.Interaction);
            if (terrainRaycastResult is { Distance: < 1.5f } &&
                Terrain.ExtractContents(terrainRaycastResult.Value.Value) != 57 &&
                Interact(terrainRaycastResult.Value))
            {
                break;
            }
        }
    }

    public void Poke(bool forceRestart)
    {
        PokingPhase = forceRestart ? 0.0001f : MathUtils.Max(0.0001f, PokingPhase);
    }

    public bool Dig(TerrainRaycastResult raycastResult, bool isEnd = false)
    {
        //在领地范围
        if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(
                raycastResult.CellFace.X,
                raycastResult.CellFace.Z,
                out Territoriy? territoriy))
        {
            if (!SubsystemBedrockBlockBehavior.AllowPlayerAction(ComponentPlayer, territoriy!))
            {
                if (Time.PeriodicEvent(1.0, 0.0))
                {
                    ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块挖掘权限", Color.Yellow, false, false);
                }

                return false;
            }
        }

        var result = false;
        _lastDigFrameIndex = Time.FrameIndex;
        var cellFace = raycastResult.CellFace;
        var cellValue = _subsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
        if (cellValue == Terrain.MakeBlockValue(BedrockBlock.Index, 0, 1))
            //不是管理员
        {
            if (ComponentPlayer is { PlayerData.ServerManager: false } && territoriy != null)
                //不是所有者
            {
                if (ComponentPlayer.PlayerData.PlayerGUID != territoriy.OwnerGuid)
                {
                    ComponentPlayer.ComponentGui.DisplaySmallMessage("只有领地拥有者才可挖掘", Color.Yellow, false, true);
                    return false;
                }
            }
        }

        var num = Terrain.ExtractContents(cellValue);
        var block = BlocksManager.Blocks[num];
        var activeBlockValue = ActiveBlockValue;
        var num2 = Terrain.ExtractContents(activeBlockValue);
        var block2 = BlocksManager.Blocks[num2];
        if (!DigCellFace.HasValue || DigCellFace.Value.X != cellFace.X || DigCellFace.Value.Y != cellFace.Y ||
            DigCellFace.Value.Z != cellFace.Z)
        {
            _digStartTime = _subsystemTime.GameTime;
            DigCellFace = cellFace;
            DigFaceChange = true;
        }

        var num3 = CalculateDigTime(cellValue, num2);
        _digProgress = num3 > 0f ? MathUtils.Saturate((float)(_subsystemTime.GameTime - _digStartTime) / num3) : 1f;
        if (!CanUseTool(activeBlockValue))
        {
            _digProgress = 0f;
            if (_subsystemTime.PeriodicGameTimeEvent(5.0, _digStartTime + 1.0))
            {
                ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    string.Format(LanguageControl.Get(Name, 1), block2.PlayerLevelRequired,
                        block2.GetDisplayName(_subsystemTerrain, activeBlockValue)), Color.White, true, true);
            }
        }

        var flag = ComponentPlayer is { ComponentInput.IsControlledByTouch: false } &&
                   _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative;
        var flag2 = flag || (_lastPokingPhase <= 0.5f && PokingPhase > 0.5f);
        ModsManager.HookAction("OnMinerDig", modLoader =>
        {
            modLoader.OnMinerDig(this, raycastResult, ref _digProgress, out var flag3);
            flag2 |= flag3;
            return false;
        });
        if (ComponentPlayer is { PlayerData.IsMainPlayer: false })
        {
            flag2 = isEnd;
            _digProgress += 0.2f;
        }

        if (_subsystemGameInfo.WorldSettings.GameMode is GameMode.Survival or GameMode.Harmless &&
            num3 >= 3f && _digProgress > 0.5f &&
            (_lastToolHintTime == 0.0 || Time.FrameStartTime - _lastToolHintTime > 300.0))
        {
            var flag3 = num3.CloseTo(CalculateDigTime(cellValue, 0));
            var num4 = FindBestInventoryToolForDigging(cellValue);
            if (num4 == 0)
            {
                if (num2 != 23 && flag3)
                {
                    ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get(Name, "11"), Color.White,
                        true, true);
                    _lastToolHintTime = Time.FrameStartTime;
                }
            }
            else if (CalculateDigTime(cellValue, Terrain.ExtractContents(num4)) < 0.5f * num3 || flag)
            {
                var displayName = BlocksManager.Blocks[Terrain.ExtractContents(num4)]
                    .GetDisplayName(_subsystemTerrain, num4);
                ComponentPlayer?.ComponentGui.DisplaySmallMessage("使用:" + displayName + "更快挖掘", Color.White, true,
                    true);
                _lastToolHintTime = Time.FrameStartTime;
            }
        }

        if (!flag2)
        {
            return result;
        }

        if (_digProgress >= 1f)
        {
            DigCellFace = null;
            if (flag)
            {
                Poke(true);
            }

            var digValue = block.GetDigValue(_subsystemTerrain, this, cellValue, activeBlockValue, raycastResult);

            _subsystemTerrain.DestroyCell(block2.ToolLevel, digValue.CellFace.X, digValue.CellFace.Y,
                digValue.CellFace.Z, digValue.Value, false, false, this);
            _subsystemSoundMaterials.PlayImpactSound(cellValue, new Vector3(cellFace.X, cellFace.Y, cellFace.Z),
                2f);
            DamageActiveTool(1);
            if (ComponentCreature.PlayerStats != null)
            {
                ComponentCreature.PlayerStats.BlocksDug++;
            }

            result = true;

#if !SERVER
            var particleSystem = block.CreateDebrisParticleSystem(_subsystemTerrain, raycastResult.HitPoint(0.1f),
                cellValue, 0.65f);
            Project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particleSystem);
#endif
        }
        else
        {
            _subsystemSoundMaterials.PlayImpactSound(cellValue, new Vector3(cellFace.X, cellFace.Y, cellFace.Z),
                1f);
        }

        return result;
    }

    public bool Place(TerrainRaycastResult raycastResult)
    {
        if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(
                raycastResult.CellFace.X,
                raycastResult.CellFace.Z,
                out Territoriy? territoriy))
        {
            if (!SubsystemBedrockBlockBehavior.AllowPlayerAction(ComponentPlayer, territoriy!))
            {
                if (Time.PeriodicEvent(1.0, 0.0))
                {
                    ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块放置权限", Color.Yellow, false, false);
                }

                return false;
            }
        }

        if (ComponentPlayer != null &&
            SubsystemBedrockBlockBehavior.Territories.ContainsKey(ComponentPlayer.PlayerGuid) &&
            ActiveBlockValue == Terrain.MakeBlockValue(BedrockBlock.Index, 0, 1))
        {
            ComponentPlayer.ComponentGui.DisplaySmallMessage("你需要挖掉原来的领地石才能放置新的", Color.Red, false, true);
            return false;
        }

        if (!Place(raycastResult, ActiveBlockValue))
        {
            return false;
        }

        if (CommonLib.WorkType != WorkType.Client)
        {
            Inventory.RemoveSlotItems(Inventory.ActiveSlotIndex, 1);
        }

        return true;
    }

    public bool Place(TerrainRaycastResult raycastResult, int value)
    {
        var num = Terrain.ExtractContents(value);
        if (_subsystemGameInfo.WorldSettings.IsBlockDiable(value))
        {
            ComponentPlayer?.ComponentGui.DisplaySmallMessage("此物品已被禁用", Color.Red, false, true);
            return false;
        }

        if (!BlocksManager.Blocks[num].Placeable)
        {
            return false;
        }

        var block = BlocksManager.Blocks[num];
        var placementData = block.GetPlacementValue(_subsystemTerrain, this, value, raycastResult);
        if (placementData.Value == 0)
        {
            return false;
        }

        var point = CellFace.FaceToPoint3(placementData.CellFace.Face);
        var num2 = placementData.CellFace.X + point.X;
        var num3 = placementData.CellFace.Y + point.Y;
        var num4 = placementData.CellFace.Z + point.Z;
        var place = false;
        ModsManager.HookAction("OnMinerPlace", modLoader =>
        {
            modLoader.OnMinerPlace(this, raycastResult, num2, num3, num4, value, out var newPlace);
            place |= newPlace;
            return false;
        });
        if (place)
        {
            return true;
        }

        var oldBlockId = _subsystemTerrain.Terrain.GetCellContents(num2, num3, num4);
        if (oldBlockId == 1)
        {
            return false;
        }

        if (oldBlockId is 233 or 232 or 229 or 226 && num == 1)
        {
            return false; //海底方块吞领地石
        }

        if (num3 is <= 0 or >= 511 || (!IsBlockPlacingAllowed(ComponentCreature.ComponentBody) &&
                                       _subsystemGameInfo.WorldSettings.GameMode > GameMode.Survival))
        {
            return false;
        }

        var flag = false;
        if (block.Collidable)
        {
            var boundingBox = ComponentCreature.ComponentBody.BoundingBox;
            boundingBox.Min += new Vector3(0.2f);
            boundingBox.Max -= new Vector3(0.2f);
            var customCollisionBoxes =
                block.GetCustomCollisionBoxes(_subsystemTerrain, placementData.Value);
            for (var i = 0; i < customCollisionBoxes.Length; i++)
            {
                var box = customCollisionBoxes[i];
                box.Min += new Vector3(num2, num3, num4);
                box.Max += new Vector3(num2, num3, num4);
                if (!boundingBox.Intersection(box))
                {
                    continue;
                }

                flag = true;
                break;
            }
        }

        if (flag)
        {
            return false;
        }

        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(
            Terrain.ExtractContents(placementData.Value), this, new Point3(num2, num3, num4));
        foreach (var behavior in blockBehaviors)
        {
            behavior.OnBlockPlaced(this, num2, num3, num4, ref placementData, value);
        }

        _subsystemTerrain.DestroyCell(0, num2, num3, num4, placementData.Value, false, false, this);

        _subsystemAudio.PlaySound("Audio/BlockPlaced", 1f, 0f,
            new Vector3(placementData.CellFace.X, placementData.CellFace.Y, placementData.CellFace.Z),
            5f, false);
        Poke(false);
        if (ComponentCreature.PlayerStats != null)
        {
            ComponentCreature.PlayerStats.BlocksPlaced++;
        }

        return true;
    }

    public bool Use(Ray3 ray)
    {
        var obj = Raycast(ray, RaycastMode.Digging);
        if (obj is TerrainRaycastResult terrainRaycast)
        {
            var cellFace = terrainRaycast.CellFace;
            if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(cellFace.X, cellFace.Z, out Territoriy? territoriy))
            {
                if (!SubsystemBedrockBlockBehavior.AllowPlayerAction(ComponentPlayer, territoriy!))
                {
                    ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块使用权限", Color.Yellow, false, false);
                    return false;
                }
            }
        }

        var num = Terrain.ExtractContents(ActiveBlockValue);
        var block = BlocksManager.Blocks[num];

        if (!CanUseTool(ActiveBlockValue))
        {
            ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                string.Format(LanguageControl.Get(Name, 1), block.PlayerLevelRequired,
                    block.GetDisplayName(_subsystemTerrain, ActiveBlockValue)), Color.White, true, true);
            Poke(false);
            return false;
        }

        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num, this, Terrain.ToCell(ray.Position));
        if (!blockBehaviors.Any(behavior => behavior.OnUse(ray, this)))
        {
            return false;
        }

        Poke(false);
        return true;
    }

    public bool Interact(TerrainRaycastResult raycastResult)
    {
        if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(
                raycastResult.CellFace.X,
                raycastResult.CellFace.Z,
                out Territoriy? territoriy))
        {
            if (!SubsystemBedrockBlockBehavior.AllowPlayerAction(ComponentPlayer, territoriy!))
            {
                ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块交互权限", Color.Yellow, false, false);
                return false;
            }
        }

        var cellContents = _subsystemTerrain.Terrain.GetCellContents(raycastResult.CellFace.X,
            raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        var blockBehaviors =
            _subsystemBlockBehaviors.GetBlockBehaviors(cellContents, this, raycastResult.CellFace.Point);
        if (!blockBehaviors.Any(behavior => behavior.OnInteract(raycastResult, this)))
        {
            return false;
        }

        if (ComponentCreature.PlayerStats != null)
        {
            ComponentCreature.PlayerStats.BlocksInteracted++;
        }

        Poke(false);
        return true;
    }

    public void Hit(ComponentBody componentBody, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!(_subsystemTime.GameTime - _lastHitTime > 0.6600000262260437))
        {
            return;
        }

        _lastHitTime = _subsystemTime.GameTime;
        var block = BlocksManager.Blocks[Terrain.ExtractContents(ActiveBlockValue)];
        if (!CanUseTool(ActiveBlockValue))
        {
            ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                string.Format(LanguageControl.Get(Name, 1), block.PlayerLevelRequired,
                    block.GetDisplayName(_subsystemTerrain, ActiveBlockValue)), Color.White, true, true);
            Poke(false);
            return;
        }

        float damage; //伤害
        float playerHitRate; //玩家命中率
        var creatureHitRate = 1f; //生物命中率
        if (ActiveBlockValue != 0)
        {
            damage = block.GetMeleePower(ActiveBlockValue) * AttackPower * _random.Float(0.8f, 1.2f);
            playerHitRate = block.GetMeleeHitProbability(ActiveBlockValue);
        }
        else
        {
            damage = AttackPower * _random.Float(0.8f, 1.2f);
            playerHitRate = 0.66f;
        }

        ModsManager.HookAction("OnMinerHit", modLoader =>
        {
            modLoader.OnMinerHit(this, componentBody, hitPoint, hitDirection, ref damage, ref playerHitRate, ref creatureHitRate,
                out var hit);
            return hit;
        });
        _subsystemAudio.PlaySound("Audio/Swoosh", 1f, _random.Float(-0.2f, 0.2f), componentBody.Position, 3f,
            false);
        var flag = _random.Bool(ComponentPlayer != null ? playerHitRate : creatureHitRate);
        if (ComponentPlayer != null)
        {
            damage *= ComponentPlayer.ComponentLevel.StrengthFactor;
        }

        if (flag)
        {
            AttackBody(componentBody, ComponentCreature, hitPoint, hitDirection, damage, true);
            DamageActiveTool(1);
        }
        else if (ComponentCreature is ComponentPlayer)
        {
            var position = hitPoint + 0.75f * hitDirection;
            var velocity = 1f * hitDirection + ComponentCreature.ComponentBody.Velocity;
            var text = LanguageControl.Get(Name, 2);
            if (CommonLib.WorkType != WorkType.Client)
            {
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(position, velocity, Color.White, text));
                var particleSystem = new HitValueParticleSystem(position, velocity, Color.White, text);
                ModsManager.HookAction("SetHitValueParticleSystem", modLoader =>
                {
                    modLoader.SetHitValueParticleSystem(particleSystem, false);
                    return false;
                });
                Project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particleSystem);
            }
        }

        if (ComponentCreature.PlayerStats != null)
        {
            ComponentCreature.PlayerStats.MeleeAttacks++;
            if (flag)
            {
                ComponentCreature.PlayerStats.MeleeHits++;
            }
        }

        Poke(false);
    }

    public bool Aim(Ray3 aim, AimState state)
    {
        var num = Terrain.ExtractContents(ActiveBlockValue);
        var block = BlocksManager.Blocks[num];
        if (!block.Aimable)
        {
            return false;
        }

        if (!CanUseTool(ActiveBlockValue))
        {
            ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                string.Format(LanguageControl.Get(Name, 1), block.PlayerLevelRequired,
                    block.GetDisplayName(_subsystemTerrain, ActiveBlockValue)), Color.White, true, true);
            Poke(false);
            return true;
        }

        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num, this, Terrain.ToCell(aim.Position));
        return blockBehaviors.Any(behavior => behavior.OnAim(aim, this, state));
    }

    public object Raycast(
        Ray3 ray,
        RaycastMode mode,
        bool raycastTerrain = true,
        bool raycastBodies = true,
        bool raycastMovingBlocks = true
    )
    {
        var reach = _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative
            ? SettingsManager.CreativeReach
            : 5f;
        var creaturePosition = ComponentCreature.ComponentCreatureModel.EyePosition;
        var start = ray.Position;
        var direction = Vector3.Normalize(ray.Direction);
        var end = ray.Position + direction * 15f;
        var startCell = Terrain.ToCell(start);
        var bodyRaycastResult = _subsystemBodies.Raycast(
            start,
            end,
            0.35f,
            (body, distance) =>
                Vector3.DistanceSquared(start + distance * direction, creaturePosition) <= reach * reach &&
                body.Entity != Entity && !body.IsChildOfBody(ComponentCreature.ComponentBody) &&
                !ComponentCreature.ComponentBody.IsChildOfBody(body) &&
                Vector3.Dot(Vector3.Normalize(body.BoundingBox.Center() - start), direction) > 0.7f
        );
        var movingBlocksRaycastResult = _subsystemMovingBlocks.Raycast(start, end, true);
        var terrainRaycastResult = _subsystemTerrain.Raycast(
            start,
            end,
            true,
            true,
            delegate(int value, float distance)
            {
                if (!(Vector3.DistanceSquared(start + distance * direction, creaturePosition) <= reach * reach))
                {
                    return false;
                }

                var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
                if (distance == 0f && block is CrossBlock &&
                    Vector3.Dot(direction, new Vector3(startCell) + new Vector3(0.5f) - start) < 0f)
                {
                    return false;
                }

                return mode switch
                {
                    RaycastMode.Digging => !block.DiggingTransparent,
                    RaycastMode.Interaction => !block.PlacementTransparent ||
                                               block.IsInteractive(_subsystemTerrain, value),
                    _ => mode == RaycastMode.Gathering && block.Gatherable
                };
            });
        var num = bodyRaycastResult?.Distance ?? float.PositiveInfinity;
        var num2 = movingBlocksRaycastResult?.Distance ?? float.PositiveInfinity;
        var num3 = terrainRaycastResult?.Distance ?? float.PositiveInfinity;
        if (num < num2 && num < num3)
        {
            return bodyRaycastResult!.Value;
        }

        if (num2 < num && num2 < num3)
        {
            return movingBlocksRaycastResult!.Value;
        }

        if (num3 < num && num3 < num2)
        {
            return terrainRaycastResult!.Value;
        }

        return new Ray3(start, direction);
    }

    public T? Raycast<T>(Ray3 ray, RaycastMode mode, bool raycastTerrain = true, bool raycastBodies = true,
        bool raycastMovingBlocks = true) where T : struct
    {
        var obj = Raycast(ray, mode, raycastTerrain, raycastBodies, raycastMovingBlocks);
        if (obj is not T result)
        {
            return null;
        }

        return result;
    }

    public void RemoveActiveTool(int removeCount)
    {
        if (CommonLib.WorkType != WorkType.Client)
        {
            Inventory.RemoveSlotItems(Inventory.ActiveSlotIndex, removeCount);
        }
    }

    public void DamageActiveTool(int damageCount)
    {
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
            CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (Inventory is InventoryDefault)
        {
            return;
        }

        var num = BlocksManager.DamageItem(ActiveBlockValue, damageCount);
        if (num != 0)
        {
            var slotCount = Inventory.GetSlotCount(Inventory.ActiveSlotIndex);
            Inventory.RemoveSlotItems(Inventory.ActiveSlotIndex, slotCount);
            if (Inventory.GetSlotCount(Inventory.ActiveSlotIndex) == 0)
            {
                Inventory.AddSlotItems(Inventory.ActiveSlotIndex, num, slotCount);
            }
        }
        else
        {
            Inventory.RemoveSlotItems(Inventory.ActiveSlotIndex, 1);
        }
    }

    public static void AttackBody(
        ComponentBody target,
        ComponentCreature? attacker,
        Vector3 hitPoint,
        Vector3 hitDirection,
        float attackPower,
        bool isMeleeAttack
    )
    {
        if (attacker is ComponentPlayer && target.Player != null)
        {
            if (!target.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.IsFriendlyFireEnabled)
            {
                attacker.Entity.FindComponent<ComponentGui>(true)!
                    .DisplaySmallMessage(LanguageControl.Get(Name, 3), Color.White, true, true);
                return;
            }
        }

        ModsManager.HookAction(
            "AttackBody",
            modLoader => modLoader.AttackBody(target, attacker, hitPoint, hitDirection, ref attackPower, isMeleeAttack)
        );

        if (attackPower > 0f)
        {
            var componentClothing = target.Entity.FindComponent<ComponentClothing>();
            if (componentClothing != null)
            {
                attackPower = componentClothing.ApplyArmorProtection(attackPower);
            }

            var componentLevel = target.Entity.FindComponent<ComponentLevel>();
            if (componentLevel != null)
            {
                attackPower /= componentLevel.ResilienceFactor;
            }

            var componentHealth = target.Entity.FindComponent<ComponentHealth>();
            if (componentHealth != null)
            {
                var num = attackPower / componentHealth.AttackResilience;
                string cause;
                if (attacker != null)
                {
                    var str = attacker.KillVerbs[_random2.Int(0, attacker.KillVerbs.Count - 1)];
                    var attackerName = attacker.DisplayName;
                    cause = string.Format(LanguageControl.Get(Name, 4), attackerName, LanguageControl.Get(Name, str));
                }
                else
                {
                    cause = _random2.Int(0, 5) switch
                    {
                        0 => LanguageControl.Get(Name, 5),
                        1 => LanguageControl.Get(Name, 6),
                        2 => LanguageControl.Get(Name, 7),
                        3 => LanguageControl.Get(Name, 8),
                        4 => LanguageControl.Get(Name, 9),
                        _ => LanguageControl.Get(Name, 10)
                    };
                }

                var health = componentHealth.Health;
                componentHealth.Injure(num, attacker, false, cause);
                if (num > 0f)
                {
                    target.Project.FindSubsystem<SubsystemAudio>(true)!.PlayRandomSound(
                        "Audio/Impacts/Body",
                        1f,
                        _random2.Float(-0.3f, 0.3f),
                        target.Position,
                        4f,
                        false
                    );
                    var num2 = (health - componentHealth.Health) * componentHealth.AttackResilience;
                    if (attacker is ComponentPlayer && num2 > 0f)
                    {
                        var text2 = (0f - num2).ToString("0", CultureInfo.InvariantCulture);
                        var position = hitPoint + 0.75f * hitDirection;
                        var velocity = 1f * hitDirection + attacker.ComponentBody.Velocity;
                        if (CommonLib.WorkType != WorkType.Client)
                        {
                            CommonLib.Net.QueuePackage(new ComponentHealthPackage(position, velocity, Color.White,
                                text2));
                            var particleSystem = new HitValueParticleSystem(position, velocity, Color.White, text2);
                            ModsManager.HookAction("SetHitValueParticleSystem", modLoader =>
                            {
                                modLoader.SetHitValueParticleSystem(particleSystem, true);
                                return false;
                            });
                            target.Project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particleSystem);
                        }
                    }
                }
            }

            var componentDamage = target.Entity.FindComponent<ComponentDamage>();
            if (componentDamage != null)
            {
                var num3 = attackPower / componentDamage.AttackResilience;
                componentDamage.Damage(num3);
                if (num3 > 0f)
                {
                    target.Project.FindSubsystem<SubsystemAudio>(true)!.PlayRandomSound(componentDamage.DamageSoundName,
                        1f, _random2.Float(-0.3f, 0.3f), target.Position, 4f, false);
                }
            }
        }

        var num4 = 0f;
        var x = 0f;
        var recalculate = false;
        if (isMeleeAttack && attacker != null)
        {
            var num5 = attackPower >= 2f ? 1.25f : 1f;
            var num6 = MathUtils.Pow(attacker.ComponentBody.Mass / target.Mass, 0.5f);
            var x2 = num5 * num6;
            num4 = 5.5f * MathUtils.Saturate(x2);
            x = 0.25f * MathUtils.Saturate(x2);
        }
        else if (attackPower > 0f)
        {
            num4 = 2f;
            x = 0.2f;
        }

        ModsManager.HookAction(
            "AttackPowerParameter",
            modLoader =>
            {
                modLoader.AttackPowerParameter(target, attacker, hitPoint, hitDirection, ref num4, ref x,
                    ref recalculate);
                return false;
            }
        );
        if (!(num4 > 0f))
        {
            return;
        }

        var impulse = num4 * Vector3.Normalize(hitDirection + _random2.Vector3(0.1f) + 0.2f * Vector3.UnitY);
        target.ApplyImpulse(impulse);
        var componentLocomotion = target.Entity.FindComponent<ComponentLocomotion>();
        if (componentLocomotion == null)
        {
            return;
        }

        if (!recalculate)
        {
            componentLocomotion.StunTime = MathUtils.Max(componentLocomotion.StunTime, x);
        }
        else
        {
            componentLocomotion.StunTime += x;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemMovingBlocks = Project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        ComponentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        ComponentPlayer = Entity.FindComponent<ComponentPlayer>();
        _componentHealth = Entity.FindComponent<ComponentHealth>(true)!;
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
        {
            var inventory = Entity.FindComponent<ComponentCreativeInventory>();
            if (inventory is not null)
            {
                Inventory = inventory;
            }
        }
        else
        {
            var inventory = Entity.FindComponent<ComponentInventory>();
            if (inventory is not null)
            {
                Inventory = inventory;
            }
        }

        AttackPower = valuesDictionary.GetValue<float>("AttackPower");
        AutoInteractRate = valuesDictionary.GetValue<float>("AutoInteractRate");
        if (string.CompareOrdinal(_subsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.4") < 0 ||
            _subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless ||
            _subsystemGameInfo.WorldSettings.GameMode == GameMode.Survival)
        {
            AutoInteractRate = 0f;
        }
    }

    public static bool IsBlockPlacingAllowed(ComponentBody componentBody)
    {
        if (componentBody.StandingOnBody != null || componentBody.StandingOnValue.HasValue)
        {
            return true;
        }

        if (componentBody.ImmersionFactor > 0.01f)
        {
            return true;
        }

        if (componentBody.ParentBody != null && IsBlockPlacingAllowed(componentBody.ParentBody))
        {
            return true;
        }

        var componentLocomotion = componentBody.Entity.FindComponent<ComponentLocomotion>();
        return componentLocomotion is { LadderValue: not null };
    }

    public float CalculateDigTime(int digValue, int toolContents)
    {
        var block = BlocksManager.Blocks[toolContents];
        var block2 = BlocksManager.Blocks[Terrain.ExtractContents(digValue)];
        var digMethod = block2.GetBlockDigMethod(digValue);
        var digResilience = block2.GetDigResilience(digValue);
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
        {
            return digResilience < float.PositiveInfinity ? 0f : float.PositiveInfinity;
        }

        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
        {
            var num = digMethod switch
            {
                BlockDigMethod.Shovel when block.ShovelPower >= 2f => block.ShovelPower,
                BlockDigMethod.Quarry when block.QuarryPower >= 2f => block.QuarryPower,
                BlockDigMethod.Hack when block.HackPower >= 2f => block.HackPower,
                _ => 0f
            };
            if (ComponentPlayer != null)
            {
                num *= ComponentPlayer.ComponentLevel.StrengthFactor;
            }

            return !(num > 0f) ? float.PositiveInfinity : MathUtils.Max(digResilience / num, 0f);
        }

        var num2 = digMethod switch
        {
            BlockDigMethod.Shovel => block.ShovelPower,
            BlockDigMethod.Quarry => block.QuarryPower,
            BlockDigMethod.Hack => block.HackPower,
            _ => 0f
        };
        if (ComponentPlayer != null)
        {
            num2 *= ComponentPlayer.ComponentLevel.StrengthFactor;
        }

        return !(num2 > 0f) ? float.PositiveInfinity : MathUtils.Max(digResilience / num2, 0f);
    }

    private bool CanUseTool(int toolValue)
    {
        if (_subsystemGameInfo.WorldSettings.GameMode == 0 ||
            !_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
        {
            return true;
        }

        if (ComponentPlayer == null)
        {
            return true;
        }

        var block = BlocksManager.Blocks[Terrain.ExtractContents(toolValue)];
        return !(ComponentPlayer.PlayerData.Level < block.PlayerLevelRequired);
    }

    private int FindBestInventoryToolForDigging(int digValue)
    {
        var result = 0;
        var num = CalculateDigTime(digValue, 0);
        foreach (var item in Entity.FindComponents<IInventory>())
        {
            if (item is null or ComponentCreativeInventory)
            {
                continue;
            }

            for (var i = 0; i < item.SlotsCount; i++)
            {
                var slotValue = item.GetSlotValue(i);
                if (!CanUseTool(slotValue))
                {
                    continue;
                }

                var num2 = CalculateDigTime(digValue, Terrain.ExtractContents(slotValue));
                if (!(num2 < num))
                {
                    continue;
                }

                num = num2;
                result = slotValue;
            }
        }

        return result;
    }
}
