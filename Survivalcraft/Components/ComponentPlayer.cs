using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Components;

public class ComponentPlayer : ComponentCreature, IUpdateable
{
    private const string _typeName = "ComponentPlayer";

    public AimEventItem CurAimEventItem;

    private readonly List<AimEventItem> _aimEvents = [];

    public DigEventItem CurDigEventItem;

    private readonly List<DigEventItem> _digEvents = [];

    public InteractEventItem CurInteractEventItem;

    private readonly List<InteractEventItem> _interactEvents = [];

    private AimEventItem _lastAimEvent;

    private DigEventItem _lastDigEvent;

    private InteractEventItem _lastInteractEvent;

    private Ray3? _aim;

    private bool _aimHintIssued;

    private bool _isAimBlocked;

    private bool _isDigBlocked;

    private double _lastActionTime;

    private bool _speedOrderBlocked;

    private byte _lastActiveSlot;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public Guid PlayerGuid;

    public PlayerData PlayerData { get; set; } = null!;

    public GameWidget GameWidget => PlayerData.GameWidget;

    public ContainerWidget GuiWidget => PlayerData.GameWidget.GuiWidget;

    public ViewWidget ViewWidget => PlayerData.GameWidget.ViewWidget;

    public ComponentGui ComponentGui { get; set; } = null!;

    public ComponentInput ComponentInput { get; set; } = null!;

    public ComponentBlockHighlight ComponentBlockHighlight { get; set; } = null!;

    public ComponentScreenOverlays ComponentScreenOverlays { get; set; } = null!;

    public ComponentAimingSights ComponentAimingSights { get; set; } = null!;

    public ComponentMiner ComponentMiner { get; set; } = null!;

    public ComponentRider ComponentRider { get; set; } = null!;

    public ComponentSleep ComponentSleep { get; set; } = null!;

    public ComponentVitalStats ComponentVitalStats { get; set; } = null!;

    public ComponentSickness ComponentSickness { get; set; } = null!;

    public ComponentFlu ComponentFlu { get; set; } = null!;

    public ComponentLevel ComponentLevel { get; set; } = null!;

    public ComponentClothing ComponentClothing { get; set; } = null!;

    public ComponentOuterClothingModel ComponentOuterClothingModel { get; set; } = null!;

    public DragHostWidget? DragHostWidget
    {
        get
        {
            field ??= GameWidget.Children.Find<DragHostWidget>(false);
            return field;
        }
    }

    public override PlayerStats PlayerStats => subsystemPlayerStats.GetPlayerStats(PlayerData.PlayerIndex);

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (PlayerData.IsMainPlayer &&
            ComponentMiner.Inventory is not InventoryDefault &&
            _lastActiveSlot != ComponentMiner.Inventory.ActiveSlotIndex)
        {
            _lastActiveSlot = (byte)ComponentMiner.Inventory.ActiveSlotIndex;
            CommonLib.Net.QueuePackage(new ComponentInventoryPackage(ComponentMiner.Inventory,
                ComponentMiner.Inventory.ActiveSlotIndex));
        }

        var playerInput = ComponentInput.PlayerInput;
        if (ComponentInput.IsControlledByTouch && _aim.HasValue)
        {
            playerInput.Look = Vector2.Zero;
        }

        if (ComponentMiner.Inventory is not InventoryDefault)
        {
            ComponentMiner.Inventory.ActiveSlotIndex += playerInput.ScrollInventory;
            if (playerInput.SelectInventorySlot.HasValue)
            {
                ComponentMiner.Inventory.ActiveSlotIndex = MathUtils.Clamp(playerInput.SelectInventorySlot.Value, 0, 9);
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
                _subsystemAudio.PlayRandomSound(
                    PlayerData.PlayerClass == PlayerClass.Male
                        ? "Audio/Creatures/MaleYellFast"
                        : "Audio/Creatures/FemaleYellFast", 0.75f, 0f, ComponentBody.Position,
                    2f, false);
                componentSteedBehavior.SpeedOrder = 1;
                _speedOrderBlocked = true;
            }
            else if (playerInput.Move.Z < -0.5f && !_speedOrderBlocked)
            {
                _subsystemAudio.PlayRandomSound(
                    PlayerData.PlayerClass == PlayerClass.Male
                        ? "Audio/Creatures/MaleYellSlow"
                        : "Audio/Creatures/FemaleYellSlow", 0.75f, 0f, ComponentBody.Position,
                    2f, false);
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
            ComponentCreatureModel.RowLeftOrder = playerInput.Move.X < -0.2f || playerInput.Move.Z > 0.2f;
            ComponentCreatureModel.RowRightOrder = playerInput.Move.X > 0.2f || playerInput.Move.Z > 0.2f;
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
                                         (SettingsManager.FlipVerticalAxis
                                             ? new Vector2(0f, -1f)
                                             : new Vector2(0f, 1f));
        var num = Terrain.ExtractContents(ComponentMiner.ActiveBlockValue);
        var block = BlocksManager.Blocks[num];

        if (Time.PeriodicEvent(0.1f, 0.0))
        {
            if (CommonLib.WorkType != WorkType.Client)
                //服务器广播玩家数据
            {
                CommonLib.Net.QueuePackage(
                    new ComponentPlayerPackage(this, ComponentPlayerPackage.PlayerAction.BodyUpdate)
                        { Except = PlayerData.Client });
            }
            else if (PlayerData.IsMainPlayer)
                //客户端广播自己的数据
            {
                CommonLib.Net.QueuePackage(new ComponentPlayerPackage(this,
                    ComponentPlayerPackage.PlayerAction.BodyUpdate));
            }
        }


        #region 服务器处理Interact，Aim，Dig事件

        if (CommonLib.WorkType != WorkType.Local && !PlayerData.IsMainPlayer)
        {
            if (_digEvents.Count > 0)
            {
                CurDigEventItem = _digEvents[0];
                _digEvents.RemoveAt(0);
            }

            if (CurDigEventItem.DigEvent == DigEvent.Start)
            {
                if (CurDigEventItem is { NetDigRay: not null, NetDigRaycast: not null })
                {
                    ComponentMiner.Dig(CurDigEventItem.NetDigRaycast.Value);
                }
            }
            else if (CurDigEventItem.DigEvent == DigEvent.Cancel)
            {
                CurDigEventItem = default;
                CurDigEventItem.NetDigRaycast = null;
                CurDigEventItem.NetDigRay = null;
                ComponentMiner.DigCellFace = null;
                ComponentMiner.DigProgress = 0f;
            }
            else if (CurDigEventItem.DigEvent == DigEvent.End)
            {
                if (CurDigEventItem.NetDigRay.HasValue)
                {
                    ComponentMiner.DigProgress = 1f;
                    if (CurDigEventItem.NetDigRaycast.HasValue)
                    {
                        ComponentMiner.Dig(CurDigEventItem.NetDigRaycast.Value, true);
                    }
                }

                CurDigEventItem = default;
                CurDigEventItem.NetDigRaycast = null;
                CurDigEventItem.NetDigRay = null;
                ComponentMiner.DigCellFace = null;
            }

            if (_aimEvents.Count > 0)
            {
                CurAimEventItem = _aimEvents[0];
                _aimEvents.RemoveAt(0);
            }

            if (CurAimEventItem.AimEvent == AimEvent.InProgress)
            {
                if (CurAimEventItem.NetAim.HasValue)
                {
                    ComponentMiner.Aim(CurAimEventItem.NetAim.Value, AimState.InProgress);
                }
            }
            else if (CurAimEventItem.AimEvent == AimEvent.Cancel)
            {
                if (CurAimEventItem.NetAim.HasValue)
                {
                    ComponentMiner.Aim(CurAimEventItem.NetAim.Value, AimState.Cancelled);
                }

                CurAimEventItem.NetAim = null;
                CurAimEventItem = default;
            }
            else if (CurAimEventItem.AimEvent == AimEvent.Complete)
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

            if (CurInteractEventItem.InteractEvent == InteractEvent.Start)
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
            else if (CurInteractEventItem.InteractEvent == InteractEvent.Place)
            {
                if (CurInteractEventItem.NetPlaceRaycast.HasValue)
                {
                    ComponentMiner.Place(CurInteractEventItem.NetPlaceRaycast.Value);
                }

                CurInteractEventItem = default;
            }
        }

        #endregion

        #region 本地处理Interact，Aim，Dig事件

        if (CommonLib.WorkType != WorkType.Local &&
            (CommonLib.WorkType == WorkType.Local || !PlayerData.IsMainPlayer))
        {
            return;
        }

        CurDigEventItem = default;
        CurInteractEventItem = default;
        CurAimEventItem = default;
        var flag = false;
        if (playerInput.Interact.HasValue && !flag &&
            _subsystemTime.GameTime - _lastActionTime > 0.33000001311302185)
        {
            CurInteractEventItem.InteractEvent = InteractEvent.Start;
            CurInteractEventItem.NetInteractRay = playerInput.Interact.Value;
            if (!ComponentMiner.Use(CurInteractEventItem.NetInteractRay))
            {
                var terrainRaycastResult =
                    ComponentMiner.Raycast<TerrainRaycastResult>(playerInput.Interact.Value,
                        RaycastMode.Interaction);
                if (terrainRaycastResult.HasValue)
                {
                    CurInteractEventItem.NetPlaceRaycast = terrainRaycastResult.Value;
                    if (!ComponentMiner.Interact(terrainRaycastResult.Value))
                    {
                        CurInteractEventItem.InteractEvent = InteractEvent.Place;
                        if (ComponentMiner.Place(terrainRaycastResult.Value))
                        {
                            _subsystemTerrain.TerrainUpdater.RequestSynchronousUpdate();
                            flag = true;
                            _isAimBlocked = true;
                        }
                    }
                    else
                    {
                        _subsystemTerrain.TerrainUpdater.RequestSynchronousUpdate();
                        flag = true;
                        _isAimBlocked = true;
                    }
                }
            }
            else
            {
                _subsystemTerrain.TerrainUpdater.RequestSynchronousUpdate();
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
                var vector =
                    GameWidget.ActiveCamera.WorldToScreen(value.Position + value.Direction, Matrix.Identity);
                var size = Window.Size;
                if (ComponentInput.IsControlledByVr || (vector.X >= size.X * 0.02f && vector.X < size.X * 0.98f &&
                                                        vector.Y >= size.Y * 0.02f && vector.Y < size.Y * 0.98f))
                {
                    _aim = value;
                    CurAimEventItem.AimEvent = AimEvent.InProgress;
                    CurAimEventItem.NetAim = value;
                    if (ComponentMiner.Aim(value, AimState.InProgress))
                    {
                        CurAimEventItem.AimEvent = AimEvent.Cancel;
                        ComponentMiner.Aim(_aim.Value, AimState.Cancelled);
                        _aim = null;
                        _isAimBlocked = true;
                    }
                    else if (!_aimHintIssued && Time.PeriodicEvent(1.0, 0.0))
                    {
                        Time.QueueTimeDelayedExecution(Time.RealTime + 3.0, delegate
                        {
                            if (_aimHintIssued || !_aim.HasValue || ComponentBody.IsSneaking)
                            {
                                return;
                            }

                            _aimHintIssued = true;
                            ComponentGui.DisplaySmallMessage(
                                LanguageControl.Get(_typeName, 1),
                                Color.White,
                                true,
                                true
                            );
                        });
                    }
                }
                else if (_aim.HasValue)
                {
                    CurAimEventItem.NetAim = _aim;
                    CurAimEventItem.AimEvent = AimEvent.Cancel;
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
                CurAimEventItem.AimEvent = AimEvent.Complete;
                ComponentMiner.Aim(_aim.Value, AimState.Completed);
                _aim = null;
                _lastActionTime = _subsystemTime.GameTime;
            }
        }

        flag |= _aim.HasValue;

        if (playerInput.Dig.HasValue && !flag && !_isDigBlocked &&
            _subsystemTime.GameTime - _lastActionTime > 0.33000001311302185)
        {
            var terrainRaycastResult2 =
                ComponentMiner.Raycast<TerrainRaycastResult>(playerInput.Dig.Value, RaycastMode.Digging);
            CurDigEventItem.DigEvent = DigEvent.Start;
            CurDigEventItem.NetDigRay = playerInput.Dig;
            CurDigEventItem.NetDigRaycast = terrainRaycastResult2;
            if (terrainRaycastResult2.HasValue)
            {
                if (ComponentMiner.Dig(terrainRaycastResult2.Value))
                {
                    CurDigEventItem.DigEvent = DigEvent.End;
                    _lastActionTime = _subsystemTime.GameTime;
                    _subsystemTerrain.TerrainUpdater.RequestSynchronousUpdate();
                }
            }
        }

        if (_lastDigEvent.DigEvent == DigEvent.Start && CurDigEventItem.DigEvent == DigEvent.None)
        {
            CurDigEventItem.DigEvent = DigEvent.Cancel;
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
                if (Vector3.Distance(bodyRaycastResult.Value.HitPoint(), ComponentCreatureModel.EyePosition) <= 2f)
                {
                    var hitPosition = bodyRaycastResult.Value.HitPoint();
                    var hitDirection = playerInput.Hit.Value.Direction;
                    ComponentMiner.Hit(bodyRaycastResult.Value.ComponentBody, hitPosition, hitDirection);
                    if (CommonLib.WorkType == WorkType.Client)
                    {
                        CommonLib.Net.QueuePackage(new ComponentPlayerPackage(this,
                            bodyRaycastResult.Value.ComponentBody, hitPosition, hitDirection));
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
            if (CommonLib.WorkType != WorkType.Client)
            {
                DoDrop();
            }
            else
            {
                CommonLib.Net.QueuePackage(new ComponentPlayerPackage(this,
                    ComponentPlayerPackage.PlayerAction.Drop));
            }
        }

        if (_lastDigEvent.DigEvent != CurDigEventItem.DigEvent || ComponentMiner.DigFaceChange)
        {
            ComponentMiner.DigFaceChange = false;
            _lastDigEvent = CurDigEventItem;
            CommonLib.Net.QueuePackage(new ComponentPlayerPackage(this,
                ComponentPlayerPackage.PlayerAction.DigEvent));
        }

        if (_lastInteractEvent.InteractEvent != CurInteractEventItem.InteractEvent)
        {
            _lastInteractEvent = CurInteractEventItem;
            CommonLib.Net.QueuePackage(new ComponentPlayerPackage(this,
                ComponentPlayerPackage.PlayerAction.InteractEvent));
        }

        if (_lastAimEvent.AimEvent != CurAimEventItem.AimEvent)
        {
            _lastAimEvent = CurAimEventItem;
            CommonLib.Net.QueuePackage(new ComponentPlayerPackage(this,
                ComponentPlayerPackage.PlayerAction.AimEvent));
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

        var terrainRaycastResult3 = ComponentMiner.Raycast<TerrainRaycastResult>(playerInput.PickBlockType.Value,
            RaycastMode.Digging, true, false, false);
        if (!terrainRaycastResult3.HasValue)
        {
            return;
        }

        var value3 = terrainRaycastResult3.Value.Value;
        value3 = Terrain.ReplaceLight(value3, 0);
        var num4 = Terrain.ExtractContents(value3);
        var block2 = BlocksManager.Blocks[num4];
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
        for (var i = 0; i < 10; i++)
        {
            if (componentCreativeInventory.GetSlotCapacity(i, num5) > 0 &&
                componentCreativeInventory.GetSlotCount(i) > 0 &&
                componentCreativeInventory.GetSlotValue(i) == num5)
            {
                num6 = i;
                break;
            }
        }

        if (num6 < 0)
        {
            for (var j = 0; j < 10; j++)
            {
                if (componentCreativeInventory.GetSlotCapacity(j, num5) > 0 &&
                    (componentCreativeInventory.GetSlotCount(j) == 0 ||
                     componentCreativeInventory.GetSlotValue(j) == 0))
                {
                    num6 = j;
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
        ComponentGui.DisplaySmallMessage(block2.GetDisplayName(_subsystemTerrain, value3), Color.White, false,
            false);
        _subsystemAudio.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f, 0f);

        #endregion
    }

    public void AddDigEvent(DigEvent digEvent, Ray3? digRay, TerrainRaycastResult? raycastResult)
    {
        _digEvents.Add(new DigEventItem
            { DigEvent = digEvent, NetDigRay = digRay, NetDigRaycast = raycastResult });
    }

    public void AddAimEvent(AimEvent aimEvent, Ray3? aimRay)
    {
        _aimEvents.Add(new AimEventItem { AimEvent = aimEvent, NetAim = aimRay });
    }

    public void AddInteractEvent(InteractEvent interactEvent, Ray3 digRay, TerrainRaycastResult? raycastResult)
    {
        _interactEvents.Add(new InteractEventItem
            { InteractEvent = interactEvent, NetInteractRay = digRay, NetPlaceRaycast = raycastResult });
    }

    public void DoDrop()
    {
        var inventory = ComponentMiner.Inventory;
        if (inventory is InventoryDefault)
        {
            return;
        }

        var slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
        var num3 = inventory.RemoveSlotItems(count: inventory.GetSlotCount(inventory.ActiveSlotIndex),
            slotIndex: inventory.ActiveSlotIndex);
        var value2 = 8f * Matrix.CreateFromQuaternion(ComponentCreatureModel.EyeRotation).Forward;
        if (slotValue == 0 || num3 == 0)
        {
            return;
        }

        var position = ComponentBody.Position + new Vector3(0f, ComponentBody.BoxSize.Y * 0.66f, 0f) +
                       0.25f * ComponentBody.Matrix.Forward;
        _subsystemPickables.AddPickable(slotValue, num3, position, value2, null);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        ComponentGui = Entity.FindComponent<ComponentGui>(true)!;
        ComponentInput = Entity.FindComponent<ComponentInput>(true)!;
        ComponentScreenOverlays = Entity.FindComponent<ComponentScreenOverlays>(true)!;
        ComponentBlockHighlight = Entity.FindComponent<ComponentBlockHighlight>(true)!;
        ComponentAimingSights = Entity.FindComponent<ComponentAimingSights>(true)!;
        ComponentMiner = Entity.FindComponent<ComponentMiner>(true)!;
        ComponentRider = Entity.FindComponent<ComponentRider>(true)!;
        ComponentSleep = Entity.FindComponent<ComponentSleep>(true)!;
        ComponentVitalStats = Entity.FindComponent<ComponentVitalStats>(true)!;
        ComponentSickness = Entity.FindComponent<ComponentSickness>(true)!;
        ComponentFlu = Entity.FindComponent<ComponentFlu>(true)!;
        ComponentLevel = Entity.FindComponent<ComponentLevel>(true)!;
        ComponentClothing = Entity.FindComponent<ComponentClothing>(true)!;
        ComponentOuterClothingModel = Entity.FindComponent<ComponentOuterClothingModel>(true)!;
        PlayerGuid = valuesDictionary.GetValue("PlayerGuid", CommonLib.Net.Self!.GUID);
        var subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        var playerData = subsystemPlayers.PlayersData.Find(d => d.PlayerGUID == PlayerGuid);
        PlayerData = playerData ?? throw new Exception($"Player data not found for guid {PlayerGuid}");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        base.Save(valuesDictionary, entityToIdMap);
        valuesDictionary.SetValue("PlayerIndex", PlayerData.PlayerIndex);
        valuesDictionary.SetValue("PlayerGuid", PlayerData.PlayerGUID);
    }

    public struct DigEventItem
    {
        public DigEvent DigEvent;

        public Ray3? NetDigRay;

        public TerrainRaycastResult? NetDigRaycast;
    }

    public struct AimEventItem
    {
        public AimEvent AimEvent;

        public Ray3? NetAim;
    }

    public struct InteractEventItem
    {
        public InteractEvent InteractEvent;

        public Ray3 NetInteractRay;

        public TerrainRaycastResult? NetPlaceRaycast;
    }
}
