using System.Xml.Linq;
using Engine.Media;
using EntitySystem.Core;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Widgets;

public class InventorySlotWidget : CanvasWidget, IDragTargetWidget
{
    private readonly BlockIconWidget _blockIconWidget;

    private ComponentPlayer? _componentPlayer;

    private readonly LabelWidget _countWidget;

    private DragMode? _dragMode;

    private readonly RectangleWidget _editOverlayWidget;

    private bool _focus;

    private readonly RectangleWidget _foodOverlayWidget;

    private readonly ValueBarWidget _healthBarWidget;

    private readonly RectangleWidget _highlightWidget;

    private readonly RectangleWidget _interactiveOverlayWidget;

    private IInventory? _inventory;

    private InventoryDragData? _inventoryDragData;

    private int _lastCount = -1;

    private readonly BevelledRectangleWidget _rectangleWidget;

    private int _slotIndex;

    private readonly LabelWidget _splitLabelWidget;

    private SubsystemTerrain? _subsystemTerrain;

    public InventorySlotWidget()
    {
        Size = new Vector2(72f, 72f);
        var array = new Widget[7];
        var obj = new BevelledRectangleWidget
        {
            BevelSize = -2f,
            DirectionalLight = 0.15f,
            CenterColor = Color.Transparent
        };
        _rectangleWidget = obj;
        array[0] = obj;
        var obj2 = new RectangleWidget
        {
            FillColor = Color.Transparent,
            OutlineColor = Color.Transparent
        };
        var rectangleWidget = obj2;
        _highlightWidget = obj2;
        array[1] = rectangleWidget;
        var obj3 = new BlockIconWidget
        {
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(2f, 2f)
        };
        var blockIconWidget = obj3;
        _blockIconWidget = obj3;
        array[2] = blockIconWidget;
        var obj4 = new LabelWidget
        {
            Font = ContentManager.Get<BitmapFont>("Fonts/Pericles"),
            FontScale = 1f,
            HorizontalAlignment = WidgetAlignment.Far,
            VerticalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(6f, 2f)
        };
        var labelWidget = obj4;
        _countWidget = obj4;
        array[3] = labelWidget;
        var obj5 = new ValueBarWidget
        {
            LayoutDirection = LayoutDirection.Vertical,
            HorizontalAlignment = WidgetAlignment.Near,
            VerticalAlignment = WidgetAlignment.Far,
            BarsCount = 3,
            FlipDirection = true,
            LitBarColor = new Color(32, 128, 0),
            UnlitBarColor = new Color(24, 24, 24, 64),
            BarSize = new Vector2(12f, 12f),
            BarSubtexture = ContentManager.Get<Subtexture>("Textures/Atlas/ProgressBar"),
            Margin = new Vector2(4f, 4f)
        };
        var valueBarWidget = obj5;
        _healthBarWidget = obj5;
        array[4] = valueBarWidget;
        var obj6 = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(3f, 3f)
        };
        var children2 = obj6.Children;
        var obj7 = new RectangleWidget
        {
            Subtexture = ContentManager.Get<Subtexture>("Textures/Atlas/InteractiveItemOverlay"),
            Size = new Vector2(13f, 14f),
            FillColor = new Color(160, 160, 160),
            OutlineColor = Color.Transparent
        };
        rectangleWidget = obj7;
        _interactiveOverlayWidget = obj7;
        children2.Add(rectangleWidget);
        var children3 = obj6.Children;
        var obj8 = new RectangleWidget
        {
            Subtexture = ContentManager.Get<Subtexture>("Textures/Atlas/EditItemOverlay"),
            Size = new Vector2(12f, 14f),
            FillColor = new Color(160, 160, 160),
            OutlineColor = Color.Transparent
        };
        rectangleWidget = obj8;
        _editOverlayWidget = obj8;
        children3.Add(rectangleWidget);
        var children4 = obj6.Children;
        var obj9 = new RectangleWidget
        {
            Subtexture = ContentManager.Get<Subtexture>("Textures/Atlas/FoodItemOverlay"),
            Size = new Vector2(11f, 14f),
            FillColor = new Color(160, 160, 160),
            OutlineColor = Color.Transparent
        };
        rectangleWidget = obj9;
        _foodOverlayWidget = obj9;
        children4.Add(rectangleWidget);
        array[5] = obj6;
        var obj10 = new LabelWidget
        {
            Text = "Split",
            Font = ContentManager.Get<BitmapFont>("Fonts/Pericles"),
            Color = new Color(255, 64, 0),
            HorizontalAlignment = WidgetAlignment.Near,
            VerticalAlignment = WidgetAlignment.Near,
            Margin = new Vector2(2f, 0f)
        };
        labelWidget = obj10;
        _splitLabelWidget = obj10;
        array[6] = labelWidget;
        Children.Add(array);
    }

    public bool HideBlockIcon { get; set; }

    public bool HideEditOverlay { get; set; }

    public bool HideInteractiveOverlay { get; set; }

    public bool HideFoodOverlay { get; set; }

    public bool HideHighlightRectangle { get; set; }

    public bool HideHealthBar { get; set; }

    public bool ProcessingOnly { get; set; }

    public Color CenterColor
    {
        get => _rectangleWidget.CenterColor;
        set => _rectangleWidget.CenterColor = value;
    }

    public Color BevelColor
    {
        get => _rectangleWidget.BevelColor;
        set => _rectangleWidget.BevelColor = value;
    }

    public Matrix? CustomViewMatrix
    {
        get => _blockIconWidget.CustomViewMatrix;
        set => _blockIconWidget.CustomViewMatrix = value;
    }

    private GameWidget? GameWidget
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            for (var parentWidget = ParentWidget; parentWidget != null; parentWidget = parentWidget.ParentWidget)
            {
                var gameWidget = parentWidget as GameWidget;
                if (gameWidget == null)
                {
                    continue;
                }

                field = gameWidget;
                break;
            }

            return field;
        }
    }

    private DragHostWidget DragHostWidget
    {
        get
        {
            field = GameWidget?.Children.Find<DragHostWidget>()!;
            return field;
        }
    }

    public void DragOver(Widget dragWidget, object data)
    {
        if (data is not InventoryDragData dragData)
        {
            return;
        }

        _inventoryDragData = dragData;
    }

    public void DragDrop(Widget dragWidget, object data)
    {
        if (data is not InventoryDragData dragData)
        {
            return;
        }

        if (_inventory != null)
        {
            HandleDragDrop(
                dragData.Inventory,
                dragData.SlotIndex,
                dragData.DragMode,
                _inventory,
                _slotIndex,
                ProcessingOnly,
                _componentPlayer
            );
        }
    }

    public void DragOut(Widget dragWidget, object data)
    {
    }

    public void DragIn(Widget dragWidget, object data)
    {
    }

    public void AssignInventorySlot(IInventory? inventory, int slotIndex)
    {
        _inventory = inventory;
        _slotIndex = slotIndex;
        _subsystemTerrain = inventory?.Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentPlayer = inventory is Component component
            ? component.Entity.FindComponent<ComponentPlayer>()
            : null;
        _blockIconWidget.DrawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
        UpdateEnvironmentData(_blockIconWidget.DrawBlockEnvironmentData);
    }

    public override void Update()
    {
        if (_inventory == null)
        {
            return;
        }

        var input = Input;
        var viewPlayer = GetViewPlayer();
        var slotValue = _inventory.GetSlotValue(_slotIndex);
        var num = Terrain.ExtractContents(slotValue);
        var block = BlocksManager.Blocks[num];
        UpdateEnvironmentData(_blockIconWidget.DrawBlockEnvironmentData);
        if (_componentPlayer != null)
        {
            _blockIconWidget.DrawBlockEnvironmentData.InWorldMatrix = _componentPlayer.ComponentBody.Matrix;
        }

        if (_focus && !input.Press.HasValue)
        {
            _focus = false;
        }
        else if (input.Tap.HasValue && HitTestGlobal(input.Tap.Value) == this)
        {
            _focus = true;
        }

        if (input.SpecialClick.HasValue && HitTestGlobal(input.SpecialClick.Value.Start) == this &&
            HitTestGlobal(input.SpecialClick.Value.End) == this)
        {
            IInventory? inventory = null;
            foreach (var item in ((ContainerWidget)RootWidget).AllChildren.OfType<InventorySlotWidget>())
            {
                if (item._inventory == null || item._inventory == _inventory || item.Input != Input ||
                    item is not { IsEnabledGlobal: true, IsVisibleGlobal: true })
                {
                    continue;
                }

                inventory = item._inventory;
                break;
            }

            if (inventory != null)
            {
                var num2 = ComponentInventoryBase.FindAcquireSlotForItem(inventory, slotValue);
                if (num2 >= 0)
                {
                    HandleMoveItem(_inventory, _slotIndex, inventory, num2, _inventory.GetSlotCount(_slotIndex));
                }
            }
        }

        if (input.Click.HasValue && HitTestGlobal(input.Click.Value.Start) == this &&
            HitTestGlobal(input.Click.Value.End) == this)
        {
            var flag = false;
            if (viewPlayer != null)
            {
                if (viewPlayer.ComponentInput.SplitSourceInventory == _inventory &&
                    viewPlayer.ComponentInput.SplitSourceSlotIndex == _slotIndex)
                {
                    viewPlayer.ComponentInput.SetSplitSourceInventoryAndSlot(null, -1);
                    flag = true;
                }
                else if (viewPlayer.ComponentInput.SplitSourceInventory != null)
                {
                    flag = HandleMoveItem(viewPlayer.ComponentInput.SplitSourceInventory,
                        viewPlayer.ComponentInput.SplitSourceSlotIndex, _inventory, _slotIndex, 1);
                    AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                }
            }

            if (!flag && _inventory.ActiveSlotIndex != _slotIndex && _slotIndex < 10)
            {
                _inventory.ActiveSlotIndex = _slotIndex;
                if (_inventory.ActiveSlotIndex == _slotIndex)
                {
                    AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                }
            }
        }

        if (!_focus || ProcessingOnly || viewPlayer == null)
        {
            return;
        }

        var hold = input.Hold;
        if (hold.HasValue && HitTestGlobal(hold.Value) == this &&
            DragHostWidget is { IsDragInProgress: false } &&
            _inventory.GetSlotCount(_slotIndex) > 0 &&
            (viewPlayer.ComponentInput.SplitSourceInventory != _inventory ||
             viewPlayer.ComponentInput.SplitSourceSlotIndex != _slotIndex))
        {
            input.Clear();
            viewPlayer.ComponentInput.SetSplitSourceInventoryAndSlot(_inventory, _slotIndex);
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }

        var drag = input.Drag;
        if (!drag.HasValue || HitTestGlobal(drag.Value) != this || DragHostWidget.IsDragInProgress)
        {
            return;
        }

        var slotCount = _inventory.GetSlotCount(_slotIndex);
        if (slotCount <= 0)
        {
            return;
        }

        var dragMode = input.DragMode;
        if (viewPlayer.ComponentInput.SplitSourceInventory == _inventory &&
            viewPlayer.ComponentInput.SplitSourceSlotIndex == _slotIndex)
        {
            dragMode = DragMode.SingleItem;
        }

        var num3 = dragMode != 0 ? 1 : slotCount;
        var subsystemTerrain = _inventory.Project.FindSubsystem<SubsystemTerrain>();
        var containerWidget =
            (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/InventoryDragWidget"), null);
        containerWidget.Children.Find<BlockIconWidget>("InventoryDragWidget.Icon")!.Value =
            Terrain.ReplaceLight(slotValue, 15);
        containerWidget.Children.Find<BlockIconWidget>("InventoryDragWidget.Icon")!.DrawBlockEnvironmentData
            .SubsystemTerrain = subsystemTerrain;
        containerWidget.Children.Find<LabelWidget>("InventoryDragWidget.Name")!.Text =
            block.GetDisplayName(subsystemTerrain, slotValue);
        containerWidget.Children.Find<LabelWidget>("InventoryDragWidget.Count")!.Text = num3.ToString();
        containerWidget.Children.Find<LabelWidget>("InventoryDragWidget.Count")!.IsVisible =
            _inventory is not ComponentCreativeInventory && _inventory is not ComponentFurnitureInventory;
        UpdateEnvironmentData(containerWidget.Children.Find<BlockIconWidget>("InventoryDragWidget.Icon")!
            .DrawBlockEnvironmentData);
        DragHostWidget.BeginDrag(
            containerWidget,
            new InventoryDragData
            {
                Inventory = _inventory,
                SlotIndex = _slotIndex,
                DragMode = dragMode
            },
            delegate { _dragMode = null; }
        );
        _dragMode = dragMode;
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (_inventory != null)
        {
            var flag = _inventory is ComponentCreativeInventory or ComponentFurnitureInventory;
            var num = _inventory.GetSlotCount(_slotIndex);
            if (!flag && _dragMode.HasValue)
            {
                num = _dragMode.Value != 0 ? MathUtils.Max(num - 1, 0) : 0;
            }

            _rectangleWidget.IsVisible = true;
            if (num > 0)
            {
                var slotValue = _inventory.GetSlotValue(_slotIndex);
                var num2 = Terrain.ExtractContents(slotValue);
                var block = BlocksManager.Blocks[num2];
                var flag2 = block.GetRotPeriod(slotValue) > 0 && block.GetDamage(slotValue) > 0;
                _blockIconWidget.Value = Terrain.ReplaceLight(slotValue, 15);
                _blockIconWidget.IsVisible = !HideBlockIcon;
                if (num != _lastCount)
                {
                    _countWidget.Text = num.ToString();
                    _lastCount = num;
                }

                _countWidget.IsVisible = num > 1 && !flag;
                _editOverlayWidget.IsVisible = !HideEditOverlay && block.Editable;
                _interactiveOverlayWidget.IsVisible = !HideInteractiveOverlay && (_subsystemTerrain != null
                    ? block.IsInteractive(_subsystemTerrain, slotValue)
                    : block.Interactive);
                _foodOverlayWidget.IsVisible = !HideFoodOverlay && block.GetRotPeriod(slotValue) > 0;
                _foodOverlayWidget.FillColor = flag2 ? new Color(128, 64, 0) : new Color(160, 160, 160);
                if (!flag && !HideHealthBar && block.Durability >= 0)
                {
                    var damage = block.GetDamage(slotValue);
                    _healthBarWidget.IsVisible = true;
                    _healthBarWidget.Value = (block.Durability - damage) / (float)block.Durability;
                }
                else
                {
                    _healthBarWidget.IsVisible = false;
                }
            }
            else
            {
                _blockIconWidget.IsVisible = false;
                _countWidget.IsVisible = false;
                _healthBarWidget.IsVisible = false;
                _editOverlayWidget.IsVisible = false;
                _interactiveOverlayWidget.IsVisible = false;
                _foodOverlayWidget.IsVisible = false;
            }

            _highlightWidget.IsVisible = !HideHighlightRectangle;
            _highlightWidget.OutlineColor = Color.Transparent;
            _highlightWidget.FillColor = Color.Transparent;
            _splitLabelWidget.IsVisible = false;
            if (_slotIndex == _inventory.ActiveSlotIndex)
            {
                _highlightWidget.OutlineColor = new Color(0, 0, 0);
                _highlightWidget.FillColor = new Color(0, 0, 0, 80);
            }

            if (IsSplitMode())
            {
                _highlightWidget.OutlineColor = new Color(255, 64, 0);
                _splitLabelWidget.IsVisible = true;
            }
        }
        else
        {
            _rectangleWidget.IsVisible = false;
            _highlightWidget.IsVisible = false;
            _blockIconWidget.IsVisible = false;
            _countWidget.IsVisible = false;
            _healthBarWidget.IsVisible = false;
            _editOverlayWidget.IsVisible = false;
            _interactiveOverlayWidget.IsVisible = false;
            _foodOverlayWidget.IsVisible = false;
            _splitLabelWidget.IsVisible = false;
        }

        IsDrawRequired = _inventoryDragData != null;
        base.MeasureOverride(parentAvailableSize);
    }

    public override void Draw(DrawContext dc)
    {
        if (_inventory != null && _inventoryDragData != null)
        {
            var slotValue = _inventoryDragData.Inventory.GetSlotValue(_inventoryDragData.SlotIndex);
            if (_inventory.GetSlotProcessCapacity(_slotIndex, slotValue) >= 0 ||
                _inventory.GetSlotCapacity(_slotIndex, slotValue) > 0)
            {
                var num = 80f * GlobalTransform.Right.Length();
                var center = Vector2.Transform(ActualSize / 2f, GlobalTransform);
                var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(100);
                flatBatch2D.QueueEllipse(center, new Vector2(num), 0f, new Color(0, 0, 0, 96) * GlobalColorTransform,
                    64);
                flatBatch2D.QueueEllipse(center, new Vector2(num - 0.5f), 0f,
                    new Color(0, 0, 0, 64) * GlobalColorTransform, 64);
                flatBatch2D.QueueEllipse(center, new Vector2(num + 0.5f), 0f,
                    new Color(0, 0, 0, 48) * GlobalColorTransform, 64);
                flatBatch2D.QueueDisc(center, new Vector2(num), 0f, new Color(0, 0, 0, 48) * GlobalColorTransform, 64);
            }
        }

        _inventoryDragData = null;
    }

    private ComponentPlayer? GetViewPlayer()
    {
        return GameWidget?.PlayerData.ComponentPlayer;
    }

    private bool IsSplitMode()
    {
        var viewPlayer = GetViewPlayer();
        if (viewPlayer == null)
        {
            return false;
        }

        if (_inventory != null && _inventory == viewPlayer.ComponentInput.SplitSourceInventory)
        {
            return _slotIndex == viewPlayer.ComponentInput.SplitSourceSlotIndex;
        }

        return false;
    }

    public static bool HandleMoveItem(
        IInventory sourceInventory,
        int sourceSlotIndex,
        IInventory targetInventory,
        int targetSlotIndex,
        int count
    )
    {
        var slotValue = sourceInventory.GetSlotValue(sourceSlotIndex);
        var slotValue2 = targetInventory.GetSlotValue(targetSlotIndex);
        var slotCount = sourceInventory.GetSlotCount(sourceSlotIndex);
        var slotCount2 = targetInventory.GetSlotCount(targetSlotIndex);


        var flag =
            sourceInventory.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.IsBlockDiable(slotValue);
        if (flag)
        {
            if (sourceInventory is not Component component)
            {
                return false;
            }

            var player = component.Entity.FindComponent<ComponentPlayer>();
            player?.ComponentGui.DisplaySmallMessage("此物品已被禁用", Color.Red, false, true);

            return false;
        }


        var sourceInventorySlot = new InventorySlot
        {
            InventoryId = sourceInventory.Id,
            SlotIndex = sourceSlotIndex
        };
        var targetInventorySlot = new InventorySlot
        {
            InventoryId = targetInventory.Id,
            SlotIndex = targetSlotIndex,
            Count = count
        };

        if (CommonLib.WorkType == WorkType.Client)
        {
            IPackage package = new ComponentInventoryPackage(sourceInventorySlot, targetInventorySlot,
                ComponentInventoryPackage.EventType.HandleMoveItem);
            CommonLib.Net.QueuePackage(package);
            //return false; // 这样搞的话客户端太难受了
        }


        if (slotCount2 != 0 && !BlocksManager.Blocks[Terrain.ExtractContents(slotValue)]
                .CanAutoStack(slotValue, slotValue2))
        {
            return false;
        }

        var num = MathUtils.Min(targetInventory.GetSlotCapacity(targetSlotIndex, slotValue) - slotCount2, slotCount,
            count);
        if (num <= 0)
        {
            return false;
        }

        var count2 = sourceInventory.RemoveSlotItems(sourceSlotIndex, num);
        targetInventory.AddSlotItems(targetSlotIndex, slotValue, count2);
        return true;
    }

    // 拖拽物品
    public static bool HandleDragDrop(
        IInventory sourceInventory,
        int sourceSlotIndex,
        DragMode dragMode,
        IInventory targetInventory,
        int targetSlotIndex,
        bool processingOnly,
        ComponentPlayer? componentPlayer = null
    )
    {
        var sourceValue = sourceInventory.GetSlotValue(sourceSlotIndex);
        var targetValue = targetInventory.GetSlotValue(targetSlotIndex);
        var sourceCount = sourceInventory.GetSlotCount(sourceSlotIndex);
        var targetCount = targetInventory.GetSlotCount(targetSlotIndex);
        var slotCapacity = targetInventory.GetSlotCapacity(targetSlotIndex, sourceValue);
        var slotProcessCapacity = targetInventory.GetSlotProcessCapacity(targetSlotIndex, sourceValue);

        var flagX =
            sourceInventory.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.IsBlockDiable(sourceValue);
        if (flagX)
        {
            if (sourceInventory is not Component component)
            {
                return false;
            }

            var player = component.Entity.FindComponent<ComponentPlayer>();
            player?.ComponentGui.DisplaySmallMessage("此物品已被禁用", Color.Red, false, true);

            return false;
        }

        var sourceInventorySlot = new InventorySlot
        {
            InventoryId = sourceInventory.Id,
            SlotIndex = sourceSlotIndex
        };
        var targetInventorySlot = new InventorySlot
        {
            InventoryId = targetInventory.Id,
            SlotIndex = targetSlotIndex
        };

        if (CommonLib.WorkType == WorkType.Client)
        {
            var package = new ComponentInventoryPackage(
                sourceInventorySlot,
                targetInventorySlot,
                ComponentInventoryPackage.EventType.HandleDragDrop)
            {
                DragMode = dragMode,
                ProcessingOnly = processingOnly
            };
            CommonLib.Net.QueuePackage(package);
        }


        if (dragMode == DragMode.SingleItem)
        {
            sourceCount = MathUtils.Min(sourceCount, 1);
        }

        var flag = false;
        if (slotProcessCapacity > 0)
        {
            // 这个可能就是拖拽衣服到角色身上，还有拖拽箭到弓上的实现
            var num6 = MathUtils.Min(sourceCount, slotProcessCapacity);
            var processCount = sourceInventory.RemoveSlotItems(sourceSlotIndex, num6);
            targetInventory.ProcessSlotItems(sourceInventory, sourceSlotIndex, targetSlotIndex, sourceValue,
                sourceCount, processCount, out var processedValue, out var processedCount);
            if (processedValue != 0 && processedCount != 0)
            {
                var count = MathUtils.Min(sourceInventory.GetSlotCapacity(sourceSlotIndex, processedValue),
                    processedCount);
                sourceInventory.AddSlotItems(sourceSlotIndex, processedValue, count);
            }

            flag = true;
        }
        else
        {
            switch (processingOnly)
            {
                case false when
                    (targetCount == 0 ||
                     BlocksManager.Blocks[Terrain.ExtractContents(sourceValue)].CanAutoStack(sourceValue, targetValue)) &&
                    targetCount < slotCapacity:
                {
                    // 这个应该跟上面的移动一样吧
                    var num2 = MathUtils.Min(slotCapacity - targetCount, sourceCount);
                    if (num2 > 0)
                    {
                        var count2 = sourceInventory.RemoveSlotItems(sourceSlotIndex, num2);
                        targetInventory.AddSlotItems(targetSlotIndex, sourceValue, count2);
                        flag = true;
                    }

                    break;
                }
                case false when targetInventory.GetSlotCapacity(targetSlotIndex, sourceValue) >= sourceCount &&
                                sourceInventory.GetSlotCapacity(sourceSlotIndex, targetValue) >= targetCount &&
                                sourceInventory.GetSlotCount(sourceSlotIndex) == sourceCount:
                {
                    var count3 = targetInventory.RemoveSlotItems(targetSlotIndex, targetCount);
                    var count4 = sourceInventory.RemoveSlotItems(sourceSlotIndex, sourceCount);
                    targetInventory.AddSlotItems(targetSlotIndex, sourceValue, count4);
                    sourceInventory.AddSlotItems(sourceSlotIndex, targetValue, count3);
                    flag = true;
                    break;
                }
            }
        }

        if (flag && componentPlayer is { PlayerData.IsMainPlayer: true }) // 一堆人移动物品的话，会导致房主出现一堆杂音，没法比只好房主没有声音了
        {
            AudioManager.PlaySound("Audio/UI/ItemMoved", 1f, 0f, 0f);
        }

        return flag;
    }

    private void UpdateEnvironmentData(DrawBlockEnvironmentData environmentData)
    {
        environmentData.SubsystemTerrain = _subsystemTerrain;
        if (_inventory is not Component component)
        {
            return;
        }

        var componentFrame = component.Entity.FindComponent<ComponentFrame>();
        if (componentFrame != null)
        {
            var point = Terrain.ToCell(componentFrame.Position);
            environmentData.InWorldMatrix = componentFrame.Matrix;
            if (_subsystemTerrain == null)
            {
                return;
            }

            environmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(point.X, point.Z);
            environmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(point.X, point.Z);
        }
        else
        {
            var componentBlockEntity = component.Entity.FindComponent<ComponentBlockEntity>();
            if (componentBlockEntity == null)
            {
                return;
            }

            var coordinates = componentBlockEntity.Coordinates;
            environmentData.InWorldMatrix = Matrix.Identity;
            if (_subsystemTerrain == null)
            {
                return;
            }

            environmentData.Temperature =
                _subsystemTerrain.Terrain.GetSeasonalTemperature(coordinates.X, coordinates.Z);
            environmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(coordinates.X, coordinates.Z);
        }
    }
}
