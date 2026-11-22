using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentMiner : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private static readonly Random _random2 = new();

    public const string Name = "ComponentMiner";

    public event Action<int>? ToolLevelRequired;

    public event Action<int, float>? ToolHintRequested;

    public event Action? ItemDisabled;

    public event Action? FriendlyFireRejected;

    public event Action<int, Vector3, float>? DigImpact;

    public event Action<BlockDigOutcome>? BlockDug;

    public event Action<Vector3>? BlockPlaced;

    public event Action<Vector3>? MeleeSwing;

    public bool DigFaceChange;

    public int DigRequestId { get; private set; }

    public int DigExpectedValue { get; private set; }

    public int PlacementRequestId { get; private set; }

    public Point3 PlacementCell { get; private set; }

    public int PlacementExpectedValue { get; private set; }

    private ComponentHealth _componentHealth = null!;

    private float _digProgress;

    private double _digStartTime;

    private long _lastDigTick;

    private double _lastHitTime;

    private float _lastPokingPhase;

    private readonly Random _random = new();

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemMovingBlocks _subsystemMovingBlocks = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public ComponentCreature ComponentCreature { get; private set; } = null!;

    public ComponentCharacter? ComponentCharacter { get; private set; }

    public float AutoInteractRate { get; private set; }

    public IInventory Inventory { get; private set; } = InventoryDefault.Default;

    public int ActiveBlockValue => Inventory.GetSlotValue(Inventory.ActiveSlotIndex);

    private float AttackPower { get; set; }

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

    public float DigProgress { get => !DigCellFace.HasValue ? 0f : _digProgress; set => _digProgress = value; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var pokeRate = _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative
            ? 0.5f / _subsystemGameInfo.WorldSettings.CreativeDigTime
            : 4f;
        _lastPokingPhase = PokingPhase;
        if (DigCellFace.HasValue || PokingPhase > 0f)
        {
            PokingPhase += pokeRate * _subsystemTime.GameTimeDelta;
            if (PokingPhase > 1f)
            {
                PokingPhase = DigCellFace.HasValue ? MathUtils.Remainder(PokingPhase, 1f) : 0f;
            }
        }

        if (DigCellFace.HasValue && WorldRuntimeContext.For(Project).Clock.TickIndex - _lastDigTick > 1)
        {
            DigCellFace = null;
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority ||
            !(_componentHealth.Health > 0f) ||
            !(AutoInteractRate > 0f) ||
            !_random.Bool(AutoInteractRate) ||
            !_subsystemTime.PeriodicGameTimeEvent(1.0, GetHashCode() % 100 / 100f))
        {
            return;
        }

        var creaturePose = ComponentCreature.ComponentCreaturePose;
        var eyePosition = creaturePose.EyePosition;
        var forwardVector = creaturePose.EyeRotation.GetForwardVector();
        for (var i = 0; i < 10; i++)
        {
            var terrainRaycastResult =
                Raycast<TerrainRaycastResult>(
                    new Ray3(eyePosition, forwardVector + _random.Vector3(0.75f)),
                    RaycastMode.Interaction
                );
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
        DigRequestId = 0;
        DigExpectedValue = 0;
        var result = false;
        _lastDigTick = WorldRuntimeContext.For(Project).Clock.TickIndex;
        var cellFace = raycastResult.CellFace;
        if (ComponentCharacter is { CharacterData.IsLocalCharacter: true } &&
            _subsystemTerrain.CellPrediction?.IsPending(cellFace.Point) == true)
        {
            DigCellFace = null;
            _digProgress = 0f;
            return false;
        }

        var cellValue = SelectDigSourceValue(
            WorldRuntimeContext.For(Project).IsAuthority,
            ComponentCharacter is { CharacterData.IsLocalCharacter: false },
            _subsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z),
            raycastResult.Value
        );
        var targetContents = Terrain.ExtractContents(cellValue);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[targetContents];
        var activeBlockValue = ActiveBlockValue;
        var toolContents = Terrain.ExtractContents(activeBlockValue);
        var block2 = WorldContentBinding.For(Project).Catalog.Blocks[toolContents];
        if (!DigCellFace.HasValue ||
            DigCellFace.Value.X != cellFace.X ||
            DigCellFace.Value.Y != cellFace.Y ||
            DigCellFace.Value.Z != cellFace.Z)
        {
            _digStartTime = _subsystemTime.GameTime;
            DigCellFace = cellFace;
            DigFaceChange = true;
        }

        var diggingContext = new MinerDiggingContext(this, raycastResult, cellValue, activeBlockValue);
        WorldRuleHooks.For(Project).Invoke(diggingContext);
        if (!WorldRuntimeContext.IsProjectActive(diggingContext.Project)) { return false; }

        if (diggingContext.Cancel)
        {
            return false;
        }

        var digDuration = CalculateDigTime(cellValue, toolContents) *
                          MathUtils.Max(diggingContext.DigTimeMultiplier, 0f);
        _digProgress = digDuration > 0f
            ? MathUtils.Saturate((float)(_subsystemTime.GameTime - _digStartTime) / digDuration)
            : 1f;
        if (!CanUseTool(activeBlockValue))
        {
            _digProgress = 0f;
            if (_subsystemTime.PeriodicGameTimeEvent(5.0, _digStartTime + 1.0))
            {
                ToolLevelRequired?.Invoke(ActiveBlockValue);
            }
        }

        var continuousCreativeDig = ComponentCharacter is { ContinuousDig: true } &&
                                    _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative;
        var applyDigStroke = continuousCreativeDig || (_lastPokingPhase <= 0.5f && PokingPhase > 0.5f);
        if (ComponentCharacter is { CharacterData.IsLocalCharacter: false })
        {
            applyDigStroke = isEnd;
            _digProgress += 0.2f;
        }

        if (_subsystemGameInfo.WorldSettings.GameMode is GameMode.Survival or GameMode.Harmless &&
            digDuration >= 3f &&
            _digProgress > 0.5f)
        {
            ToolHintRequested?.Invoke(cellValue, digDuration);
        }

        if (!applyDigStroke)
        {
            return result;
        }

        if (_digProgress >= 1f)
        {
            DigCellFace = null;
            if (continuousCreativeDig)
            {
                Poke(true);
            }

            var digValue = block.GetDigValue(_subsystemTerrain, cellValue, activeBlockValue, raycastResult);

            if (!WorldRuntimeContext.For(Project).IsAuthority &&
                ComponentCharacter is { CharacterData.IsLocalCharacter: true } &&
                digValue.CellFace.Point == cellFace.Point)
            {
                DigExpectedValue = cellValue;
                DigRequestId = _subsystemTerrain.CellPrediction!.Begin(
                    cellFace.Point,
                    cellValue,
                    digValue.Value,
                    Time.RealTime
                );
            }

            _subsystemTerrain.DestroyCell(
                block2.ToolLevel,
                digValue.CellFace.X,
                digValue.CellFace.Y,
                digValue.CellFace.Z,
                digValue.Value,
                false,
                false,
                this
            );
            ApplyDigRuleEffects(_subsystemTerrain, cellValue, digValue);
            DigImpact?.Invoke(cellValue, new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 2f);
            DamageActiveTool(1);
            if (ComponentCreature.CharacterStats != null)
            {
                ComponentCreature.CharacterStats.BlocksDug++;
            }

            result = true;

            BlockDug?.Invoke(
                new BlockDigOutcome(
                    cellValue,
                    digValue.Value,
                    digValue.CellFace,
                    raycastResult.HitPoint(0.1f),
                    digValue.Kind,
                    block.AllowsDestructionDebris(cellValue, digValue.Value)
                )
            );
        }
        else
        {
            DigImpact?.Invoke(cellValue, new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 1f);
        }

        return result;
    }

    public bool Place(TerrainRaycastResult raycastResult)
    {
        if (!Place(raycastResult, ActiveBlockValue))
        {
            return false;
        }

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            Inventory.RemoveSlotItems(Inventory.ActiveSlotIndex, 1);
        }

        return true;
    }

    public bool Place(TerrainRaycastResult raycastResult, int value)
    {
        PlacementRequestId = 0;
        PlacementCell = default;
        PlacementExpectedValue = 0;
        var placingContext = new BlockPlacingContext(this, raycastResult, value);
        WorldRuleHooks.For(Project).Invoke(placingContext);
        if (!WorldRuntimeContext.IsProjectActive(placingContext.Project)) { return false; }

        if (placingContext.Cancel)
        {
            return false;
        }

        value = placingContext.Value;
        var placedContents = Terrain.ExtractContents(value);
        if (BlockAvailability.IsBlockDisabled(
                WorldContentBinding.For(Project).Catalog,
                _subsystemGameInfo.WorldSettings.DisableBlocks,
                value
            ))
        {
            ItemDisabled?.Invoke();
            return false;
        }

        if (!WorldContentBinding.For(Project).Catalog.Blocks[placedContents].Placeable)
        {
            return false;
        }

        var block = WorldContentBinding.For(Project).Catalog.Blocks[placedContents];
        var placementData = block.GetPlacementValue(
            _subsystemTerrain,
            Matrix.CreateFromQuaternion(ComponentCreature.ComponentCreaturePose.EyeRotation).Forward,
            value,
            raycastResult
        );
        if (placementData.Rejection != BlockPlacementRejection.None)
        {
            if (ComponentCharacter is { } character)
            {
                return RejectPlacement(
                    Project.FindSubsystem<SubsystemMessages>(true)!.Service,
                    WorldRuntimeContext.For(Project).IsAuthority,
                    character.IsLocallyControlled,
                    character.CharacterData.ClientId,
                    placementData.Rejection
                );
            }

            return false;
        }

        if (placementData.Value == 0)
        {
            return false;
        }

        var point = CellFace.FaceToPoint3(placementData.CellFace.Face);
        var placementX = placementData.CellFace.X + point.X;
        var placementY = placementData.CellFace.Y + point.Y;
        var placementZ = placementData.CellFace.Z + point.Z;
        var oldBlockId = _subsystemTerrain.Terrain.GetCellContents(placementX, placementY, placementZ);
        if (oldBlockId == 1)
        {
            return false;
        }

        if (placementY is <= 0 or >= 256 ||
            (!IsBlockPlacingAllowed(ComponentCreature.ComponentBody) &&
             _subsystemGameInfo.WorldSettings.GameMode > GameMode.Survival))
        {
            return false;
        }

        var overlapsBody = false;
        if (block.Collidable)
        {
            var boundingBox = ComponentCreature.ComponentBody.BoundingBox;
            boundingBox.Min += new Vector3(0.2f);
            boundingBox.Max -= new Vector3(0.2f);
            var customCollisionBoxes =
                block.GetCustomCollisionBoxes(_subsystemTerrain, placementData.Value);
            foreach (var box in customCollisionBoxes)
            {
                var boxLocal = box;
                boxLocal.Min += new Vector3(placementX, placementY, placementZ);
                boxLocal.Max += new Vector3(placementX, placementY, placementZ);
                if (!boundingBox.Intersection(boxLocal))
                {
                    continue;
                }

                overlapsBody = true;
                break;
            }
        }

        if (overlapsBody)
        {
            return false;
        }

        var blockPlacedContext = new BlockPlacedContext(this, placementX, placementY, placementZ, placementData, value);
        WorldRuleHooks.For(Project).Invoke(blockPlacedContext);
        if (!WorldRuntimeContext.IsProjectActive(blockPlacedContext.Project)) { return false; }

        if (blockPlacedContext.Cancel)
        {
            return false;
        }

        placementData = blockPlacedContext.PlacementData;
        value = blockPlacedContext.Value;

        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(placementData.Value));
        foreach (var behavior in blockBehaviors)
        {
            behavior.OnBlockPlaced(this, placementX, placementY, placementZ, ref placementData, value);
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority &&
            ComponentCharacter is { CharacterData.IsLocalCharacter: true })
        {
            PlacementCell = new Point3(placementX, placementY, placementZ);
            PlacementExpectedValue = _subsystemTerrain.Terrain.GetCellValue(placementX, placementY, placementZ);
            PlacementRequestId = _subsystemTerrain.CellPrediction!.Begin(
                PlacementCell,
                PlacementExpectedValue,
                placementData.Value,
                Time.RealTime
            );
        }

        _subsystemTerrain.DestroyCell(0, placementX, placementY, placementZ, placementData.Value, false, false, this);

        BlockPlaced?.Invoke(new Vector3(placementData.CellFace.X, placementData.CellFace.Y, placementData.CellFace.Z));
        Poke(false);
        if (ComponentCreature.CharacterStats != null)
        {
            ComponentCreature.CharacterStats.BlocksPlaced++;
        }

        return true;
    }

    public bool Use(Ray3 ray)
    {
        var heldContents = Terrain.ExtractContents(ActiveBlockValue);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[heldContents];

        var useContext = new BlockUseContext(ray, this, ActiveBlockValue);
        WorldRuleHooks.For(Project).Invoke(useContext);
        if (!WorldRuntimeContext.IsProjectActive(useContext.Project)) { return false; }

        if (useContext.Cancel)
        {
            return useContext.Handled;
        }

        if (!CanUseTool(ActiveBlockValue))
        {
            ToolLevelRequired?.Invoke(ActiveBlockValue);
            Poke(false);
            return false;
        }

        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(heldContents);
        if (!blockBehaviors.Any(behavior => behavior.OnUse(ray, this)) && !useContext.Handled)
        {
            return false;
        }

        Poke(false);
        return true;
    }

    public bool Interact(TerrainRaycastResult raycastResult)
    {
        var cellValue = _subsystemTerrain.Terrain.GetCellValue(
            raycastResult.CellFace.X,
            raycastResult.CellFace.Y,
            raycastResult.CellFace.Z
        );
        var interactContext = new BlockInteractContext(raycastResult, this, cellValue);
        WorldRuleHooks.For(Project).Invoke(interactContext);
        if (!WorldRuntimeContext.IsProjectActive(interactContext.Project)) { return false; }

        if (interactContext.Cancel)
        {
            return interactContext.Handled;
        }

        var cellContents = Terrain.ExtractContents(cellValue);
        var blockBehaviors =
            _subsystemBlockBehaviors.GetBlockBehaviors(cellContents);
        if (!blockBehaviors.Any(behavior => behavior.OnInteract(raycastResult, this)) && !interactContext.Handled)
        {
            return false;
        }

        if (ComponentCreature.CharacterStats != null)
        {
            ComponentCreature.CharacterStats.BlocksInteracted++;
        }

        Poke(false);
        return true;
    }

    public void Hit(ComponentBody componentBody, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !ReferenceEquals(componentBody.Project, Project))
        {
            return;
        }

        if (!(_subsystemTime.GameTime - _lastHitTime > 0.6600000262260437))
        {
            return;
        }

        _lastHitTime = _subsystemTime.GameTime;
        var block = WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(ActiveBlockValue)];
        if (!CanUseTool(ActiveBlockValue))
        {
            ToolLevelRequired?.Invoke(ActiveBlockValue);
            Poke(false);
            return;
        }

        MeleeSwing?.Invoke(componentBody.Position);
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            if (ComponentCharacter != null &&
                componentBody.Character != null &&
                !_subsystemGameInfo.WorldSettings.IsFriendlyFireEnabled)
            {
                FriendlyFireRejected?.Invoke();
            }

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

        var hitSucceeded = _random.Bool(ComponentCharacter != null ? playerHitRate : creatureHitRate);
        if (ComponentCharacter != null)
        {
            damage *= ComponentCharacter.ComponentLevel.StrengthFactor;
        }

        if (hitSucceeded)
        {
            AttackBody(componentBody, ComponentCreature, hitPoint, hitDirection, damage, true);
            DamageActiveTool(1);
        }
        else if (ComponentCreature is ComponentCharacter)
        {
            var position = hitPoint + 0.75f * hitDirection;
            var velocity = 1f * hitDirection + ComponentCreature.ComponentBody.Velocity;
            var text = new LocalizedText(Name, "2", "2");
            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                NetworkSender.SendNearPoint(
                    Project,
                    position,
                    24f,
                    new CharacterHitEffectPackage
                        { Position = position, Velocity = velocity, Color = Color.White, Text = text }
                );
            }
        }

        if (ComponentCreature.CharacterStats != null)
        {
            ComponentCreature.CharacterStats.MeleeAttacks++;
            if (hitSucceeded)
            {
                ComponentCreature.CharacterStats.MeleeHits++;
            }

            if (ComponentCharacter?.CharacterData.ControllerConnection is { IsConnected: true } owner)
            {
                NetworkSender.SendTo(
                    owner,
                    CharacterPackageBuilder.Capture(
                        ComponentCharacter,
                        ComponentCharacterPackage.CharacterAction.SyncStat
                    )
                );
            }
        }

        Poke(false);
    }

    public bool Aim(Ray3 aim, AimState state)
    {
        var heldContents = Terrain.ExtractContents(ActiveBlockValue);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[heldContents];
        if (!block.Aimable)
        {
            return false;
        }

        if (!CanUseTool(ActiveBlockValue))
        {
            ToolLevelRequired?.Invoke(ActiveBlockValue);
            Poke(false);
            return true;
        }

        var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(heldContents);
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
            ? _subsystemGameInfo.WorldSettings.CreativeReach
            : 5f;
        var creaturePosition = ComponentCreature.ComponentCreaturePose.EyePosition;
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
                body.Entity != Entity &&
                !body.IsChildOfBody(ComponentCreature.ComponentBody) &&
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

                var block = WorldContentBinding.For(Project).Catalog
                    .Blocks[Terrain.ExtractContents(value)];
                if (distance == 0f &&
                    block is CrossBlock &&
                    Vector3.Dot(direction, new Vector3(startCell) + new Vector3(0.5f) - start) < 0f)
                {
                    return false;
                }

                return mode switch
                {
                    RaycastMode.Digging => !block.IsDiggingTransparent(value),
                    RaycastMode.Interaction => !block.PlacementTransparent ||
                                               block.IsInteractive(_subsystemTerrain, value),
                    _ => mode == RaycastMode.Gathering && block.Gatherable
                };
            },
            includePredictedBlocks: mode == RaycastMode.Digging &&
                                    ComponentCharacter is { CharacterData.IsLocalCharacter: true }
        );
        var bodyDistance = bodyRaycastResult?.Distance ?? float.PositiveInfinity;
        var movingBlockDistance = movingBlocksRaycastResult?.Distance ?? float.PositiveInfinity;
        var terrainDistance = terrainRaycastResult?.Distance ?? float.PositiveInfinity;
        if (bodyDistance < movingBlockDistance && bodyDistance < terrainDistance)
        {
            return bodyRaycastResult!.Value;
        }

        if (movingBlockDistance < bodyDistance && movingBlockDistance < terrainDistance)
        {
            return movingBlocksRaycastResult!.Value;
        }

        if (terrainDistance < bodyDistance && terrainDistance < movingBlockDistance)
        {
            return terrainRaycastResult!.Value;
        }

        return new Ray3(start, direction);
    }

    public T? Raycast<T>(
        Ray3 ray,
        RaycastMode mode,
        bool raycastTerrain = true,
        bool raycastBodies = true,
        bool raycastMovingBlocks = true
    ) where T : struct
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
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            Inventory.RemoveSlotItems(Inventory.ActiveSlotIndex, removeCount);
        }
    }

    public void DamageActiveTool(int damageCount)
    {
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
            !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (Inventory is InventoryDefault)
        {
            return;
        }

        var damagedValue = WorldContentBinding.For(Project).Catalog
            .DamageItem(ActiveBlockValue, damageCount);
        if (damagedValue != 0)
        {
            var slotCount = Inventory.GetSlotCount(Inventory.ActiveSlotIndex);
            Inventory.RemoveSlotItems(Inventory.ActiveSlotIndex, slotCount);
            if (Inventory.GetSlotCount(Inventory.ActiveSlotIndex) == 0)
            {
                Inventory.AddSlotItems(Inventory.ActiveSlotIndex, damagedValue, slotCount);
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
        if (!WorldRuntimeContext.IsProjectActive(target.Project) ||
            !WorldRuntimeContext.For(target.Project).IsAuthority)
        {
            return;
        }

        if (attacker is ComponentCharacter && target.Character != null)
        {
            if (!target.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.IsFriendlyFireEnabled)
            {
                attacker.Entity.FindComponent<ComponentMiner>()?.FriendlyFireRejected?.Invoke();
                return;
            }
        }

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
                var healthDamage = attackPower / componentHealth.AttackResilience;
                LocalizedText cause;
                if (attacker != null)
                {
                    var str = attacker.KillVerbs[_random2.Int(0, attacker.KillVerbs.Count - 1)];
                    var attackerName = attacker.DisplayName;
                    cause = LocalizedText.Format(
                        new LocalizedText(Name, "4", "{0} {1}"),
                        attackerName,
                        new LocalizedText(Name, str, str)
                    );
                }
                else
                {
                    cause = _random2.Int(0, 5) switch
                    {
                        0 => new LocalizedText(Name, "5", "5"),
                        1 => new LocalizedText(Name, "6", "6"),
                        2 => new LocalizedText(Name, "7", "7"),
                        3 => new LocalizedText(Name, "8", "8"),
                        4 => new LocalizedText(Name, "9", "9"),
                        _ => new LocalizedText(Name, "10", "10")
                    };
                }

                var health = componentHealth.Health;
                componentHealth.Injure(healthDamage, attacker, false, cause);
                if (healthDamage > 0f)
                {
                    var actualDamage = (health - componentHealth.Health) * componentHealth.AttackResilience;
                    var position = hitPoint + 0.75f * hitDirection;
                    NetworkSender.SendNearPoint(
                        target.Project,
                        target.Position,
                        24f,
                        new CharacterHitEffectPackage
                        {
                            Position = position,
                            Velocity = attacker is null ? hitDirection : hitDirection + attacker.ComponentBody.Velocity,
                            Color = Color.White,
                            Text = attacker is ComponentCharacter && actualDamage > 0f
                                ? LocalizedText.Literal((-actualDamage).ToString("0", CultureInfo.InvariantCulture))
                                : LocalizedText.Empty,
                            PlayBodyImpact = true,
                            BodyPosition = target.Position
                        }
                    );
                }
            }

            var componentDamage = target.Entity.FindComponent<ComponentDamage>();
            if (componentDamage != null)
            {
                var durabilityDamage = attackPower / componentDamage.AttackResilience;
                componentDamage.Damage(durabilityDamage, isAttack: true);
            }
        }

        var impulseMagnitude = 0f;
        var x = 0f;
        var recalculate = false;
        if (isMeleeAttack && attacker != null)
        {
            var attackStrengthFactor = attackPower >= 2f ? 1.25f : 1f;
            var massRatioFactor = MathUtils.Pow(attacker.ComponentBody.Mass / target.Mass, 0.5f);
            var x2 = attackStrengthFactor * massRatioFactor;
            impulseMagnitude = 5.5f * MathUtils.Saturate(x2);
            x = 0.25f * MathUtils.Saturate(x2);
        }
        else if (attackPower > 0f)
        {
            impulseMagnitude = 2f;
            x = 0.2f;
        }

        if (!(impulseMagnitude > 0f))
        {
            return;
        }

        var impulse = impulseMagnitude *
                      Vector3.Normalize(hitDirection + _random2.Vector3(0.1f) + 0.2f * Vector3.UnitY);
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
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        ComponentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        ComponentCharacter = Entity.FindComponent<ComponentCharacter>();
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
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless ||
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
        var block = WorldContentBinding.For(Project).Catalog.Blocks[toolContents];
        var block2 = WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(digValue)];
        var digMethod = block2.GetBlockDigMethod(digValue);
        var digResilience = block2.GetDigResilience(digValue);
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
        {
            return digResilience < float.PositiveInfinity ? 0f : float.PositiveInfinity;
        }

        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
        {
            var adventureDigPower = digMethod switch
            {
                BlockDigMethod.Shovel when block.ShovelPower >= 2f => block.ShovelPower,
                BlockDigMethod.Quarry when block.QuarryPower >= 2f => block.QuarryPower,
                BlockDigMethod.Hack when block.HackPower >= 2f => block.HackPower,
                _ => 0f
            };
            if (ComponentCharacter != null)
            {
                adventureDigPower *= ComponentCharacter.ComponentLevel.StrengthFactor;
            }

            return !(adventureDigPower > 0f)
                ? float.PositiveInfinity
                : MathUtils.Max(digResilience / adventureDigPower, 0f);
        }

        var digPower = digMethod switch
        {
            BlockDigMethod.Shovel => block.ShovelPower,
            BlockDigMethod.Quarry => block.QuarryPower,
            BlockDigMethod.Hack => block.HackPower,
            _ => 0f
        };
        if (ComponentCharacter != null)
        {
            digPower *= ComponentCharacter.ComponentLevel.StrengthFactor;
        }

        return !(digPower > 0f) ? float.PositiveInfinity : MathUtils.Max(digResilience / digPower, 0f);
    }

    private bool CanUseTool(int toolValue)
    {
        if (_subsystemGameInfo.WorldSettings.GameMode == 0 ||
            !_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
        {
            return true;
        }

        if (ComponentCharacter == null)
        {
            return true;
        }

        var block = WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(toolValue)];
        return !(ComponentCharacter.CharacterData.Level < block.PlayerLevelRequired);
    }

    public int FindBestInventoryToolForDigging(int digValue)
    {
        var result = 0;
        var bestDigTime = CalculateDigTime(digValue, 0);
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

                var slotDigTime = CalculateDigTime(digValue, Terrain.ExtractContents(slotValue));
                if (!(slotDigTime < bestDigTime))
                {
                    continue;
                }

                bestDigTime = slotDigTime;
                result = slotValue;
            }
        }

        return result;
    }

    internal static bool RejectPlacement(
        GameMessageService messages,
        bool isAuthority,
        bool isLocallyControlled,
        byte clientId,
        BlockPlacementRejection rejection
    )
    {
        var text = rejection switch
        {
            BlockPlacementRejection.MagnetLimit => "Too many magnets",
            _ => throw new InvalidOperationException("Unknown block placement rejection.")
        };
        var message = GameMessage.System(text, presentation: GameMessagePresentation.Toast);
        if (isAuthority)
        {
            messages.Publish(message, [clientId], includePublisher: false);
        }
        else if (isLocallyControlled)
        {
            // Rejected predictions send no placement request, so no authority feedback will follow.
            messages.DisplayLocal(message);
        }

        return false;
    }

    internal static int SelectDigSourceValue(
        bool isAuthority,
        bool isRemoteCharacter,
        int currentValue,
        int claimedValue
    )
    {
        return !isAuthority && isRemoteCharacter ? claimedValue : currentValue;
    }

    internal static bool ApplyDigRuleEffects(SubsystemTerrain terrain, int oldValue, BlockDigData dig)
    {
        if (!WorldRuntimeContext.IsProjectActive(terrain.Project) ||
            !WorldRuntimeContext.For(terrain.Project).IsAuthority ||
            dig.Kind != BlockDigOutcomeKind.LeavesShaken ||
            !DeciduousLeavesBlock.IsShakeTransition(oldValue, dig.Value))
        {
            return false;
        }

        var cell = dig.CellFace.Point;
        var installed = terrain.Terrain.GetCellValue(cell.X, cell.Y, cell.Z);
        if (Terrain.NormalizeCellValue(installed) != Terrain.NormalizeCellValue(dig.Value)) { return false; }

        terrain.Project.FindSubsystem<SubsystemDeciduousLeavesBlockBehavior>(true)!
            .CreateFallenLeaves(cell, true);
        return true;
    }
}
