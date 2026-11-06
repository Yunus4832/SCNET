using System.Xml.Linq;

using Game.Network.Transport;

namespace Game.Widgets;

public class ClothingWidget : CanvasWidget
{
    private readonly StackPanelWidget _clothingStack;

    private readonly ComponentCharacter _componentCharacter;

    private readonly ComponentClothingPresentation _presentation;

    private readonly CharacterModelWidget _innerClothingModelWidget;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly CharacterModelWidget _outerClothingModelWidget;

    private readonly ButtonWidget _sleepButton;

    private readonly ButtonWidget _vitalStatsButton;

    public ClothingWidget(ComponentCharacter componentCharacter)
    {
        _componentCharacter = componentCharacter;
        _presentation = componentCharacter.Entity.FindComponent<ComponentClothingPresentation>(true)!;
        var node = ContentManager.Get<XElement>("Widgets/ClothingWidget");
        LoadContents(this, node);
        _clothingStack = Children.Find<StackPanelWidget>("ClothingStack")!;
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _vitalStatsButton = Children.Find<ButtonWidget>("VitalStatsButton")!;
        _sleepButton = Children.Find<ButtonWidget>("SleepButton")!;
        _innerClothingModelWidget = Children.Find<CharacterModelWidget>("InnerClothingModel")!;
        _outerClothingModelWidget = Children.Find<CharacterModelWidget>("OuterClothingModel")!;
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
            inventorySlotWidget.AssignInventorySlot(_componentCharacter.ComponentClothing, i);
            inventorySlotWidget.HideEditOverlay = true;
            inventorySlotWidget.HideInteractiveOverlay = true;
            inventorySlotWidget.HideFoodOverlay = true;
            inventorySlotWidget.HideHighlightRectangle = true;
            inventorySlotWidget.HideBlockIcon = true;
            inventorySlotWidget.HideHealthBar =
                _componentCharacter.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode ==
                GameMode.Creative;
            _clothingStack.Children.Add(inventorySlotWidget);
        }

        var num = PlayerInventoryLayout.GetBackpackStartIndex(_componentCharacter.ComponentMiner.Inventory);
        for (var j = 0; j < _inventoryGrid.RowsCount; j++)
        {
            for (var k = 0; k < _inventoryGrid.ColumnsCount; k++)
            {
                var inventorySlotWidget2 = new InventorySlotWidget();
                inventorySlotWidget2.AssignInventorySlot(_componentCharacter.ComponentMiner.Inventory, num++);
                _inventoryGrid.Children.Add(inventorySlotWidget2);
                _inventoryGrid.SetWidgetCell(inventorySlotWidget2, new Point2(k, j));
            }
        }

        RefreshModels();
    }

    private void RefreshModels()
    {
        _presentation.UpdateRenderTargets();
        _innerClothingModelWidget.CharacterSex = _componentCharacter.CharacterData.CharacterSex;
        _innerClothingModelWidget.CharacterSkinTexture = _presentation.InnerClothedTexture;
        _outerClothingModelWidget.CharacterSex = _componentCharacter.CharacterData.CharacterSex;
        _outerClothingModelWidget.OuterClothingTexture = _presentation.OuterClothedTexture;
    }

    public override void Update()
    {
        IsVisible = _componentCharacter.IsAddedToProject;
        if (!IsVisible) { return; }

        RefreshModels();
        if (_vitalStatsButton.IsClicked)
        {
            _componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget =
                new VitalStatsWidget(_componentCharacter);
        }

        if (!_sleepButton.IsClicked)
        {
            return;
        }

        NetworkSender.SendToServer(
            WorldRuntimeContext.For(_componentCharacter.Project),
            new SleepRequestPackage
                { EntityId = _componentCharacter.ComponentSleep.Entity.EntityId }
        );
    }
}
