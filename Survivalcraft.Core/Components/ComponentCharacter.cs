using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components.Behavior;

namespace Game.Components;

public class ComponentCharacter : ComponentCreature, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public AimEventItem CurAimEventItem;

    private readonly List<AimEventItem> _aimEvents = [];

    public DigEventItem CurDigEventItem;

    private readonly List<DigEventItem> _digEvents = [];

    public InteractEventItem CurInteractEventItem;

    private readonly List<InteractEventItem> _interactEvents = [];

    private AimEventItem _lastAimEvent;

    private DigEventItem _lastDigEvent;

    private Ray3? _aim;

    private bool _isAimBlocked;

    private bool _isDigBlocked;

    private double _lastActionTime;

    private bool _speedOrderBlocked;

    private byte _lastActiveSlot;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemUpdate _subsystemUpdate = null!;

    public Guid CharacterId;

    public event Action<bool>? SteedSpeedFeedback;

    public event Action<int>? CreativeBlockPicked;

    public event Action? AimHintRequested;

    public bool IsAiming => _aim.HasValue;

    public CharacterData CharacterData { get; set; } = null!;

    private CharacterControlInput _controlInput;

    public void SetControlInput(CharacterControlInput input)
    {
        if (!IsLocallyControlled)
        {
            throw new InvalidOperationException("Only a local client controller submits local input.");
        }

        _controlInput = input;
    }

    public bool AutoJumpEnabled => IsLocallyControlled && _controlInput.AutoJumpEnabled;

    public bool ContinuousDig => _controlInput.ContinuousDig;

    public bool UseLookDirectedCreativeFlight => _controlInput.UseLookDirectedCreativeFlight;

    public void ApplyRemoteFlightControl(bool useLookDirectedCreativeFlight)
    {
        if (!IsLocallyControlled)
        {
            _controlInput.UseLookDirectedCreativeFlight = useLookDirectedCreativeFlight;
        }
    }

    public void ClearControlInput() => _controlInput = default;

    public bool IsLocallyControlled =>
        WorldRuntimeContext.For(Project).HasPresentation &&
        CharacterData?.IsLocalCharacter == true;

    public ComponentMiner ComponentMiner { get; set; } = null!;

    public ComponentRider ComponentRider { get; set; } = null!;

    public ComponentSleep ComponentSleep { get; set; } = null!;

    public ComponentVitalStats ComponentVitalStats { get; set; } = null!;

    public ComponentSickness ComponentSickness { get; set; } = null!;

    public ComponentFlu ComponentFlu { get; set; } = null!;

    public ComponentLevel ComponentLevel { get; set; } = null!;

    public ComponentClothing ComponentClothing { get; set; } = null!;

    public override CharacterStats CharacterStats =>
        subsystemCharacterStats.GetCharacterStats(CharacterData.CharacterId);

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CharacterData.IsLocalCharacter &&
            ComponentMiner.Inventory is not InventoryDefault &&
            _lastActiveSlot != ComponentMiner.Inventory.ActiveSlotIndex)
        {
            _lastActiveSlot = (byte)ComponentMiner.Inventory.ActiveSlotIndex;
            var package = new InventoryActiveSlotPackage
            {
                InventoryID = ComponentMiner.Inventory.Id, ActiveSlot = ComponentMiner.Inventory.ActiveSlotIndex
            };
            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                NetworkSender.SendToObservers(Entity, package);
            }
            else
            {
                NetworkSender.SendToServer(WorldRuntimeContext.For(Project), package);
            }
        }

        var playerInput = _controlInput;
        if (playerInput.SuppressLookWhileAiming && _aim.HasValue)
        {
            playerInput.Look = Vector2.Zero;
        }

        if (ComponentMiner.Inventory is not InventoryDefault)
        {
            if (ComponentMiner.Inventory is IPagedInventory pagedInventory)
            {
                pagedInventory.ChangeHotbarPage(playerInput.ScrollInventoryPage);
                if (playerInput.SelectInventoryPage.HasValue)
                {
                    pagedInventory.SelectHotbarPage(playerInput.SelectInventoryPage.Value);
                }

                pagedInventory.ActiveSlotIndex += playerInput.ScrollInventory;
                if (playerInput.SelectInventorySlot.HasValue)
                {
                    pagedInventory.SelectHotbarPageSlot(playerInput.SelectInventorySlot.Value);
                }
            }
        }

        ComponentSteedBehavior? componentSteedBehavior = null;
        ComponentBoat? componentBoat = null;
        var mount = ComponentRider.Mount;
        if (mount != null)
        {
            componentSteedBehavior = mount.Entity.FindComponent<ComponentSteedBehavior>();
            componentBoat = mount.Entity.FindComponent<ComponentBoat>();
        }

        if (componentSteedBehavior != null)
        {
            if (playerInput.Move.Z > 0.5f && !_speedOrderBlocked)
            {
                SteedSpeedFeedback?.Invoke(true);
                componentSteedBehavior.SpeedOrder = 1;
                _speedOrderBlocked = true;
            }
            else if (playerInput.Move.Z < -0.5f && !_speedOrderBlocked)
            {
                SteedSpeedFeedback?.Invoke(false);
                componentSteedBehavior.SpeedOrder = -1;
                _speedOrderBlocked = true;
            }
            else if (MathUtils.Abs(playerInput.Move.Z) <= 0.25f)
            {
                _speedOrderBlocked = false;
            }

            componentSteedBehavior.TurnOrder = playerInput.Move.X;
            componentSteedBehavior.JumpOrder = playerInput.Jump ? 1 : 0;
            ComponentLocomotion.LookOrder = new Vector2(playerInput.Look.X, 0f);
        }
        else if (componentBoat != null)
        {
            componentBoat.TurnOrder = playerInput.Move.X;
            componentBoat.MoveOrder = playerInput.Move.Z;
            ComponentLocomotion.LookOrder = new Vector2(playerInput.Look.X, 0f);
            ComponentCreaturePose.RowLeftOrder = playerInput.Move.X < -0.2f || playerInput.Move.Z > 0.2f;
            ComponentCreaturePose.RowRightOrder = playerInput.Move.X > 0.2f || playerInput.Move.Z > 0.2f;
        }
        else
        {
            ComponentLocomotion.WalkOrder = ComponentBody.IsSneaking
                ? 0.66f * new Vector2(playerInput.SneakMove.X, playerInput.SneakMove.Z)
                : new Vector2(playerInput.Move.X, playerInput.Move.Z);
            ComponentLocomotion.FlyOrder = new Vector3(0f, playerInput.Move.Y, 0f);
            ComponentLocomotion.TurnOrder = playerInput.Look * new Vector2(1f, 0f);
            ComponentLocomotion.JumpOrder = MathUtils.Max(playerInput.Jump ? 1 : 0, ComponentLocomotion.JumpOrder);
        }

        ComponentLocomotion.LookOrder += playerInput.Look *
                                         (playerInput.FlipLookY
                                             ? new Vector2(0f, -1f)
                                             : new Vector2(0f, 1f));
        var num = Terrain.ExtractContents(ComponentMiner.ActiveBlockValue);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[num];

        if (_subsystemUpdate.IsLastUpdateInFrame && Time.PeriodicEvent(0.1f, 0.0))
        {
            if (WorldRuntimeContext.For(Project).IsAuthority)
                //服务器广播玩家数据
            {
                NetworkSender.SendToObservers(
                    Entity,
                    CharacterPackageBuilder.Capture(this, ComponentCharacterPackage.CharacterAction.BodyUpdate),
                    CharacterData.ControllerConnection
                );
            }
            else if (CharacterData.IsLocalCharacter)
                //客户端向服务端提交自己的数据
            {
                NetworkSender.SendToServer(
                    WorldRuntimeContext.For(Project),
                    CharacterPackageBuilder.Capture(
                        this,
                        ComponentCharacterPackage.CharacterAction.BodyUpdate
                    )
                );
            }
        }

        #region 处理非本地角色的交互事件

        if (!CharacterData.IsLocalCharacter)
        {
            UpdateRemoteDig();

            if (_aimEvents.Count > 0)
            {
                CurAimEventItem = _aimEvents[0];
                _aimEvents.RemoveAt(0);
            }

            if (CurAimEventItem.AimEvent == SyncAction.Start)
            {
                if (CurAimEventItem.NetAim.HasValue)
                {
                    ComponentMiner.Aim(CurAimEventItem.NetAim.Value, AimState.InProgress);
                }
            }
            else if (CurAimEventItem.AimEvent == SyncAction.Cancel)
            {
                if (CurAimEventItem.NetAim.HasValue)
                {
                    ComponentMiner.Aim(CurAimEventItem.NetAim.Value, AimState.Cancelled);
                }

                CurAimEventItem.NetAim = null;
                CurAimEventItem = default;
            }
            else if (CurAimEventItem.AimEvent == SyncAction.Complete)
            {
                if (CurAimEventItem.NetAim.HasValue)
                {
                    ComponentMiner.Aim(CurAimEventItem.NetAim.Value, AimState.Completed);
                }

                _isAimBlocked = false;
                CurAimEventItem.NetAim = null;
                CurAimEventItem = default;
            }

            if (_interactEvents.Count > 0)
            {
                CurInteractEventItem = _interactEvents[0];
                _interactEvents.RemoveAt(0);
            }

            if (CurInteractEventItem.InteractEvent == InteractionMessageKind.Use)
            {
                if (!ComponentMiner.Use(CurInteractEventItem.NetInteractRay))
                {
                    if (CurInteractEventItem.NetPlaceRaycast.HasValue)
                    {
                        ComponentMiner.Interact(CurInteractEventItem.NetPlaceRaycast.Value);
                    }
                }

                CurInteractEventItem = default;
            }
            else if (CurInteractEventItem.InteractEvent == InteractionMessageKind.Place)
            {
                if (CurInteractEventItem.NetPlaceRaycast.HasValue)
                {
                    var cell = CurInteractEventItem.PlacementCell;
                    var value = _subsystemTerrain.Terrain.GetCellValue(cell.X, cell.Y, cell.Z);
                    if (CurInteractEventItem.RequestId == 0 ||
                        Terrain.ReplaceLight(value, 0) == Terrain.ReplaceLight(CurInteractEventItem.ExpectedValue, 0))
                    {
                        ComponentMiner.Place(CurInteractEventItem.NetPlaceRaycast.Value);
                    }

                    CompleteCellPrediction(CurInteractEventItem.RequestId, cell);
                }

                CurInteractEventItem = default;
            }
        }

        #endregion

        #region 处理本地角色输入

        if (!CharacterData.IsLocalCharacter)
        {
            return;
        }

        CurDigEventItem = default;
        CurInteractEventItem = default;
        CurAimEventItem = default;
        var flag = false;
        if (playerInput.Interact.HasValue &&
            !flag &&
            _subsystemTime.GameTime - _lastActionTime > 0.33000001311302185)
        {
            CurInteractEventItem.InteractEvent = InteractionMessageKind.Use;
            CurInteractEventItem.NetInteractRay = playerInput.Interact.Value;
            if (!ComponentMiner.Use(CurInteractEventItem.NetInteractRay))
            {
                var terrainRaycastResult =
                    ComponentMiner.Raycast<TerrainRaycastResult>(
                        playerInput.Interact.Value,
                        RaycastMode.Interaction
                    );
                if (terrainRaycastResult.HasValue)
                {
                    CurInteractEventItem.NetPlaceRaycast = terrainRaycastResult.Value;
                    if (!ComponentMiner.Interact(terrainRaycastResult.Value))
                    {
                        CurInteractEventItem.InteractEvent = InteractionMessageKind.Place;
                        if (ComponentMiner.Place(terrainRaycastResult.Value))
                        {
                            CurInteractEventItem.RequestId = ComponentMiner.PlacementRequestId;
                            CurInteractEventItem.PlacementCell = ComponentMiner.PlacementCell;
                            CurInteractEventItem.ExpectedValue = ComponentMiner.PlacementExpectedValue;
                            _subsystemTerrain.NotifyTerrainInteraction();
                            flag = true;
                            _isAimBlocked = true;
                        }
                    }
                    else
                    {
                        _subsystemTerrain.NotifyTerrainInteraction();
                        flag = true;
                        _isAimBlocked = true;
                    }
                }
            }
            else
            {
                _subsystemTerrain.NotifyTerrainInteraction();
                flag = true;
                _isAimBlocked = true;
            }
        }

        var num2 = _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ? 0.1f : 1.4f;
        if (playerInput.Aim.HasValue && block.Aimable && _subsystemTime.GameTime - _lastActionTime > num2)
        {
            if (!_isAimBlocked)
            {
                var value = playerInput.Aim.Value;
                if (playerInput.IsAimDirectionAllowed)
                {
                    _aim = value;
                    CurAimEventItem.AimEvent = SyncAction.Start;
                    CurAimEventItem.NetAim = value;
                    if (ComponentMiner.Aim(value, AimState.InProgress))
                    {
                        CurAimEventItem.AimEvent = SyncAction.Cancel;
                        ComponentMiner.Aim(_aim.Value, AimState.Cancelled);
                        _aim = null;
                        _isAimBlocked = true;
                    }
                    else if (Time.PeriodicEvent(1, 0))
                    {
                        AimHintRequested?.Invoke();
                    }
                }
                else if (_aim.HasValue)
                {
                    CurAimEventItem.NetAim = _aim;
                    CurAimEventItem.AimEvent = SyncAction.Cancel;
                    ComponentMiner.Aim(_aim.Value, AimState.Cancelled);
                    _aim = null;
                    _isAimBlocked = true;
                }
            }
        }
        else
        {
            _isAimBlocked = false;
            if (_aim.HasValue)
            {
                CurAimEventItem.NetAim = _aim;
                CurAimEventItem.AimEvent = SyncAction.Complete;
                ComponentMiner.Aim(_aim.Value, AimState.Completed);
                _aim = null;
                _lastActionTime = _subsystemTime.GameTime;
            }
        }

        flag |= _aim.HasValue;

        if (playerInput.Dig.HasValue &&
            !flag &&
            !_isDigBlocked &&
            _subsystemTime.GameTime - _lastActionTime > 0.33000001311302185)
        {
            var terrainRaycastResult2 =
                ComponentMiner.Raycast<TerrainRaycastResult>(playerInput.Dig.Value, RaycastMode.Digging);
            if (terrainRaycastResult2.HasValue &&
                _subsystemTerrain.CellPrediction?.IsPending(terrainRaycastResult2.Value.CellFace.Point) != true)
            {
                CurDigEventItem.DigEvent = SyncAction.Start;
                CurDigEventItem.NetDigRay = playerInput.Dig;
                CurDigEventItem.NetDigRaycast = terrainRaycastResult2;
                if (ComponentMiner.Dig(terrainRaycastResult2.Value))
                {
                    CurDigEventItem.DigEvent = SyncAction.Complete;
                    CurDigEventItem.RequestId = ComponentMiner.DigRequestId;
                    CurDigEventItem.ExpectedValue = ComponentMiner.DigExpectedValue;
                    _lastActionTime = _subsystemTime.GameTime;
                    _subsystemTerrain.NotifyTerrainInteraction();
                }
            }
        }

        if (_lastDigEvent.DigEvent == SyncAction.Start && CurDigEventItem.DigEvent == SyncAction.None)
        {
            CurDigEventItem.DigEvent = SyncAction.Cancel;
        }

        #region 玩家攻击事件

        if (playerInput.Hit.HasValue && !flag && _subsystemTime.GameTime - _lastActionTime > 0.33000001311302185)
        {
            var bodyRaycastResult =
                ComponentMiner.Raycast<BodyRaycastResult>(playerInput.Hit.Value, RaycastMode.Interaction);
            if (bodyRaycastResult.HasValue)
            {
                flag = true;
                _isDigBlocked = true;
                if (Vector3.Distance(bodyRaycastResult.Value.HitPoint(), ComponentCreaturePose.EyePosition) <= 2f)
                {
                    var hitPosition = bodyRaycastResult.Value.HitPoint();
                    var hitDirection = playerInput.Hit.Value.Direction;
                    ComponentMiner.Hit(bodyRaycastResult.Value.ComponentBody, hitPosition, hitDirection);
                    if (!WorldRuntimeContext.For(Project).IsAuthority)
                    {
                        NetworkSender.SendToServer(
                            WorldRuntimeContext.For(Project),
                            CharacterPackageBuilder.Capture(
                                this,
                                bodyRaycastResult.Value.ComponentBody,
                                hitPosition,
                                hitDirection
                            )
                        );
                    }
                }
            }
        }

        #endregion

        if (!playerInput.Dig.HasValue)
        {
            _isDigBlocked = false;
        }

        if (playerInput.Drop && ComponentMiner.Inventory is not InventoryDefault)
        {
            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                DoDrop();
            }
            else
            {
                NetworkSender.SendToServer(
                    WorldRuntimeContext.For(Project),
                    CharacterPackageBuilder.Capture(
                        this,
                        ComponentCharacterPackage.CharacterAction.Drop
                    )
                );
            }
        }

        if (_lastDigEvent.DigEvent != CurDigEventItem.DigEvent || ComponentMiner.DigFaceChange)
        {
            ComponentMiner.DigFaceChange = false;
            _lastDigEvent = CurDigEventItem;
            PublishAction(ComponentCharacterPackage.CharacterAction.DigEvent);
        }

        if (CurInteractEventItem.InteractEvent != InteractionMessageKind.None)
        {
            PublishAction(ComponentCharacterPackage.CharacterAction.InteractEvent);
        }

        if (_lastAimEvent.AimEvent != CurAimEventItem.AimEvent)
        {
            _lastAimEvent = CurAimEventItem;
            PublishAction(ComponentCharacterPackage.CharacterAction.AimEvent);
        }

        if (!playerInput.PickBlockType.HasValue || flag)
        {
            return;
        }

        var componentCreativeInventory = ComponentMiner.Inventory as ComponentCreativeInventory;
        if (componentCreativeInventory == null)
        {
            return;
        }

        var terrainRaycastResult3 = ComponentMiner.Raycast<TerrainRaycastResult>(
            playerInput.PickBlockType.Value,
            RaycastMode.Digging,
            true,
            false,
            false
        );
        if (!terrainRaycastResult3.HasValue)
        {
            return;
        }

        var value3 = terrainRaycastResult3.Value.Value;
        value3 = Terrain.ReplaceLight(value3, 0);
        var num4 = Terrain.ExtractContents(value3);
        var block2 = WorldContentBinding.For(Project).Catalog.Blocks[num4];
        var num5 = 0;
        var creativeValues = block2.GetCreativeValues();
        if (block2.GetCreativeValues().Contains(value3))
        {
            num5 = value3;
        }

        if (num5 == 0 && !block2.NonDuplicable)
        {
            var list = new List<BlockDropValue>();
            block2.GetDropValues(_subsystemTerrain, value3, 0, int.MaxValue, list, out _);
            if (list.Count > 0 && list[0].Count > 0)
            {
                num5 = list[0].Value;
            }
        }

        if (num5 == 0)
        {
            num5 = creativeValues.FirstOrDefault();
        }

        if (num5 == 0)
        {
            return;
        }

        var num6 = -1;
        for (var i = 0; i < componentCreativeInventory.VisibleSlotsCount; i++)
        {
            var slotIndex = componentCreativeInventory.GetHotbarSlotIndex(i);
            if (slotIndex >= 0 &&
                componentCreativeInventory.GetSlotCapacity(slotIndex, num5) > 0 &&
                componentCreativeInventory.GetSlotCount(slotIndex) > 0 &&
                componentCreativeInventory.GetSlotValue(slotIndex) == num5)
            {
                num6 = slotIndex;
                break;
            }
        }

        if (num6 < 0)
        {
            for (var i = 0; i < componentCreativeInventory.VisibleSlotsCount; i++)
            {
                var slotIndex = componentCreativeInventory.GetHotbarSlotIndex(i);
                if (slotIndex >= 0 &&
                    componentCreativeInventory.GetSlotCapacity(slotIndex, num5) > 0 &&
                    (componentCreativeInventory.GetSlotCount(slotIndex) == 0 ||
                     componentCreativeInventory.GetSlotValue(slotIndex) == 0))
                {
                    num6 = slotIndex;
                    break;
                }
            }
        }

        if (num6 < 0)
        {
            num6 = componentCreativeInventory.ActiveSlotIndex;
        }

        componentCreativeInventory.RemoveSlotItems(num6, int.MaxValue);
        componentCreativeInventory.AddSlotItems(num6, num5, 1);
        componentCreativeInventory.ActiveSlotIndex = num6;
        CreativeBlockPicked?.Invoke(value3);

        #endregion
    }

    private void PublishAction(ComponentCharacterPackage.CharacterAction action)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            NetworkSender.SendToObservers(
                Entity,
                CharacterPackageBuilder.Capture(this, action),
                CharacterData.ControllerConnection
            );
        }
        else
        {
            NetworkSender.SendToServer(WorldRuntimeContext.For(Project), CharacterPackageBuilder.Capture(this, action));
        }
    }

    private void UpdateRemoteDig()
    {
        if (_digEvents.Count > 0)
        {
            CurDigEventItem = _digEvents[0];
            _digEvents.RemoveAt(0);
        }

        if (CurDigEventItem.DigEvent == SyncAction.Start)
        {
            if (TryResolveRemoteDigHit(CurDigEventItem, out var hit))
            {
                ComponentMiner.Dig(hit);
            }
            else
            {
                ComponentMiner.DigCellFace = null;
                ComponentMiner.DigProgress = 0f;
            }
        }
        else if (CurDigEventItem.DigEvent == SyncAction.Cancel)
        {
            CurDigEventItem = default;
            ComponentMiner.DigCellFace = null;
            ComponentMiner.DigProgress = 0f;
        }
        else if (CurDigEventItem.DigEvent == SyncAction.Complete)
        {
            var completed = CurDigEventItem;
            try
            {
                if (TryResolveRemoteDigHit(completed, out var hit))
                {
                    var cell = hit.CellFace.Point;
                    var value = _subsystemTerrain.Terrain.GetCellValue(cell.X, cell.Y, cell.Z);
                    if (completed.RequestId == 0 ||
                        Terrain.ReplaceLight(value, 0) == Terrain.ReplaceLight(completed.ExpectedValue, 0))
                    {
                        ComponentMiner.DigProgress = 1f;
                        ComponentMiner.Dig(hit, true);
                    }
                }
            }
            finally
            {
                CurDigEventItem = default;
                ComponentMiner.DigCellFace = null;
                if (completed.NetDigRaycast is { } claimed)
                {
                    CompleteCellPrediction(completed.RequestId, claimed.CellFace.Point);
                }
            }
        }
    }

    private bool TryResolveRemoteDigHit(DigEventItem item, out TerrainRaycastResult hit)
    {
        hit = default;
        if (item.NetDigRay is not { } ray || item.NetDigRaycast is not { } claimed)
        {
            return false;
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            hit = claimed;
            return true;
        }

        static bool Finite(Vector3 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        if (!Finite(ray.Position) ||
            !Finite(ray.Direction) ||
            !float.IsFinite(ray.Direction.LengthSquared()) ||
            ray.Direction.LengthSquared() <= 0f ||
            !Finite(claimed.Ray.Position) ||
            !Finite(claimed.Ray.Direction) ||
            !float.IsFinite(claimed.Distance) ||
            claimed.Distance < 0f ||
            claimed.CellFace.Face is < 0 or > 5)
        {
            return false;
        }

        if (ComponentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging) is not { } actual ||
            actual.CellFace != claimed.CellFace)
        {
            return false;
        }

        hit = actual;
        return true;
    }

    private void CompleteCellPrediction(int requestId, Point3 cell)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority ||
            requestId <= 0 ||
            CharacterData.ControllerConnection is not { } client)
        {
            return;
        }

        var chunk = _subsystemTerrain.AuthoritativeTerrain.GetChunkAtCell(cell.X, cell.Z, false);
        NetworkSender.SendTo(
            client,
            new TerrainCellEditCompletedPackage
            {
                EditRequestId = requestId, EditedCell = cell,
                EditContentVersion = chunk?.NetworkContentRevision + 1 ?? 0
            }
        );
    }

    public void AddDigEvent(
        SyncAction digEvent,
        Ray3? digRay,
        TerrainRaycastResult? raycastResult,
        int requestId,
        int expectedValue
    )
    {
        _digEvents.Add(
            new DigEventItem
            {
                DigEvent = digEvent,
                NetDigRay = digRay,
                NetDigRaycast = raycastResult,
                RequestId = requestId,
                ExpectedValue = expectedValue
            }
        );
    }

    public void AddAimEvent(SyncAction aimEvent, Ray3? aimRay)
    {
        _aimEvents.Add(new AimEventItem { AimEvent = aimEvent, NetAim = aimRay });
    }

    public void AddInteractEvent(
        InteractionMessageKind interactEvent,
        Ray3 digRay,
        TerrainRaycastResult? raycastResult,
        int requestId,
        Point3 placementCell,
        int expectedValue
    )
    {
        _interactEvents.Add(
            new InteractEventItem
            {
                InteractEvent = interactEvent,
                NetInteractRay = digRay,
                NetPlaceRaycast = raycastResult,
                RequestId = requestId,
                PlacementCell = placementCell,
                ExpectedValue = expectedValue
            }
        );
    }

    public void DoDrop()
    {
        var inventory = ComponentMiner.Inventory;
        if (inventory is InventoryDefault)
        {
            return;
        }

        var slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
        var num3 = inventory.RemoveSlotItems(
            count: inventory.GetSlotCount(inventory.ActiveSlotIndex),
            slotIndex: inventory.ActiveSlotIndex
        );
        var value2 = 8f * Matrix.CreateFromQuaternion(ComponentCreaturePose.EyeRotation).Forward;
        if (slotValue == 0 || num3 == 0)
        {
            return;
        }

        var position = ComponentBody.Position +
                       new Vector3(0f, ComponentBody.BoxSize.Y * 0.66f, 0f) +
                       0.25f * ComponentBody.Matrix.Forward;
        _subsystemPickables.AddPickable(slotValue, num3, position, value2, null);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
        ComponentMiner = Entity.FindComponent<ComponentMiner>(true)!;
        ComponentRider = Entity.FindComponent<ComponentRider>(true)!;
        ComponentSleep = Entity.FindComponent<ComponentSleep>(true)!;
        ComponentVitalStats = Entity.FindComponent<ComponentVitalStats>(true)!;
        ComponentSickness = Entity.FindComponent<ComponentSickness>(true)!;
        ComponentFlu = Entity.FindComponent<ComponentFlu>(true)!;
        ComponentLevel = Entity.FindComponent<ComponentLevel>(true)!;
        ComponentClothing = Entity.FindComponent<ComponentClothing>(true)!;
        CharacterId = valuesDictionary.GetValue<Guid>("CharacterId");
        var subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        var characterData = subsystemCharacters.Characters.Find(d => d.CharacterId == CharacterId);
        CharacterData = characterData ?? throw new Exception($"Character data not found for id {CharacterId}");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        base.Save(valuesDictionary, entityToIdMap);
        valuesDictionary.SetValue("CharacterId", CharacterData.CharacterId);
    }

    public struct DigEventItem
    {
        public int RequestId;

        public int ExpectedValue;

        public SyncAction DigEvent;

        public Ray3? NetDigRay;

        public TerrainRaycastResult? NetDigRaycast;
    }

    public struct AimEventItem
    {
        public SyncAction AimEvent;

        public Ray3? NetAim;
    }

    public struct InteractEventItem
    {
        public int RequestId;

        public Point3 PlacementCell;

        public int ExpectedValue;

        public InteractionMessageKind InteractEvent;

        public Ray3 NetInteractRay;

        public TerrainRaycastResult? NetPlaceRaycast;
    }
}
