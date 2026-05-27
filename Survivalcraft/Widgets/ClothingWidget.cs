using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

public class ClothingWidget : CanvasWidget
{
    private readonly StackPanelWidget _clothingStack;

    private readonly ComponentPlayer _componentPlayer;

    private readonly PlayerModelWidget _innerClothingModelWidget;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly PlayerModelWidget _outerClothingModelWidget;

    private readonly ButtonWidget _sleepButton;

    private readonly ButtonWidget _vitalStatsButton;

    public ClothingWidget(ComponentPlayer componentPlayer)
    {
        _componentPlayer = componentPlayer;
        var node = ContentManager.Get<XElement>("Widgets/ClothingWidget");
        LoadContents(this, node);
        _clothingStack = Children.Find<StackPanelWidget>("ClothingStack")!;
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _vitalStatsButton = Children.Find<ButtonWidget>("VitalStatsButton")!;
        _sleepButton = Children.Find<ButtonWidget>("SleepButton")!;
        _innerClothingModelWidget = Children.Find<PlayerModelWidget>("InnerClothingModel")!;
        _outerClothingModelWidget = Children.Find<PlayerModelWidget>("OuterClothingModel")!;
        for (var i = 0; i < 4; i++)
        {
            var inventorySlotWidget = new InventorySlotWidget();
            var y = float.PositiveInfinity;
            if (i == 0)
            {
                y = 68f;
            }

            if (i == 3)
            {
                y = 54f;
            }

            inventorySlotWidget.Size = new Vector2(float.PositiveInfinity, y);
            inventorySlotWidget.BevelColor = Color.Transparent;
            inventorySlotWidget.CenterColor = Color.Transparent;
            inventorySlotWidget.AssignInventorySlot(_componentPlayer.ComponentClothing, i);
            inventorySlotWidget.HideEditOverlay = true;
            inventorySlotWidget.HideInteractiveOverlay = true;
            inventorySlotWidget.HideFoodOverlay = true;
            inventorySlotWidget.HideHighlightRectangle = true;
            inventorySlotWidget.HideBlockIcon = true;
            inventorySlotWidget.HideHealthBar =
                _componentPlayer.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode ==
                GameMode.Creative;
            _clothingStack.Children.Add(inventorySlotWidget);
        }

        var num = 10;
        for (var j = 0; j < _inventoryGrid.RowsCount; j++)
        for (var k = 0; k < _inventoryGrid.ColumnsCount; k++)
        {
            var inventorySlotWidget2 = new InventorySlotWidget();
            inventorySlotWidget2.AssignInventorySlot(_componentPlayer.ComponentMiner.Inventory, num++);
            _inventoryGrid.Children.Add(inventorySlotWidget2);
            _inventoryGrid.SetWidgetCell(inventorySlotWidget2, new Point2(k, j));
        }

        _innerClothingModelWidget.PlayerClass = _componentPlayer.PlayerData.PlayerClass;
        _innerClothingModelWidget.CharacterSkinTexture = _componentPlayer.ComponentClothing.InnerClothedTexture;
        _outerClothingModelWidget.PlayerClass = _componentPlayer.PlayerData.PlayerClass;
        _outerClothingModelWidget.OuterClothingTexture = _componentPlayer.ComponentClothing.OuterClothedTexture;
    }

    public override void Update()
    {
        if (_vitalStatsButton.IsClicked)
        {
            _componentPlayer.ComponentGui.ModalPanelWidget = new VitalStatsWidget(_componentPlayer);
        }

        if (!_sleepButton.IsClicked)
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            CommonLib.Net.QueuePackage(new ComponentSleepPackage(_componentPlayer.ComponentSleep,
                ComponentSleepPackage.EventType.SleepRequest, true));
        }
        else
        {
            if (!_componentPlayer.ComponentSleep.CanSleep(out var reason))
            {
                _componentPlayer.ComponentGui.DisplaySmallMessage(reason, Color.White, false, false);
            }
            else
            {
                _componentPlayer.ComponentSleep.Sleep(true);
            }
        }
    }
}
