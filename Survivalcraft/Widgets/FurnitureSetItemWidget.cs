using System.Xml.Linq;

using Engine.Graphics;

using Game.Network;
using Game.Network.Packages;

namespace Game.Widgets;

public class FurnitureSetItemWidget : CanvasWidget, IDragTargetWidget
{
    private readonly FurnitureInventoryPanel _furnitureInventoryPanel;

    private readonly FurnitureSet _furnitureSet;

    private bool _highlighted;

    public FurnitureSetItemWidget(FurnitureInventoryPanel furnitureInventoryWidget, FurnitureSet furnitureSet)
    {
        _furnitureInventoryPanel = furnitureInventoryWidget;
        _furnitureSet = furnitureSet;
        var node = ContentManager.Get<XElement>("Widgets/FurnitureSetItemWidget");
        LoadContents(this, node);
        var labelWidget = Children.Find<LabelWidget>("FurnitureSetItem.Name")!;
        var labelWidget2 = Children.Find<LabelWidget>("FurnitureSetItem.DesignsCount")!;
        labelWidget.Text = furnitureSet.Name;
        var count = CountFurnitureDesigns();
        labelWidget2.Text = string.Format(LanguageControl.AutoGet(this, 1), count);
    }

    public void DragDrop(Widget dragWidget, object data)
    {
        var furnitureDesign = GetFurnitureDesign(data);
        if (furnitureDesign == null)
        {
            return;
        }

        _furnitureInventoryPanel.SubsystemFurnitureBlockBehavior.AddToFurnitureSet(furnitureDesign,
            _furnitureSet);
        _furnitureInventoryPanel.Invalidate();
        CommonLib.Net.QueuePackage(new FurniturePackage(furnitureDesign, _furnitureSet));
    }

    public void DragOver(Widget dragWidget, object data)
    {
        _highlighted = GetFurnitureDesign(data) != null;
    }

    public void DragOut(Widget dragWidget, object data)
    {
    }

    public void DragIn(Widget dragWidget, object data)
    {
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = _highlighted;
        base.MeasureOverride(parentAvailableSize);
    }

    public override void Draw(DrawContext dc)
    {
        if (!_highlighted)
        {
            return;
        }

        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(100, DepthStencilState.None);
        var count = flatBatch2D.TriangleVertices.Count;
        flatBatch2D.QueueQuad(Vector2.Zero, ActualSize, 0f, new Color(128, 128, 128, 128));
        flatBatch2D.TransformTriangles(GlobalTransform, count);
        _highlighted = false;
    }

    private FurnitureDesign? GetFurnitureDesign(object dragData)
    {
        if (dragData is not InventoryDragData inventoryDragData)
        {
            return null;
        }

        var slotValue = inventoryDragData.Inventory.GetSlotValue(inventoryDragData.SlotIndex);
        if (Terrain.ExtractContents(slotValue) != 227)
        {
            return null;
        }

        var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(slotValue));
        return _furnitureInventoryPanel.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);

    }

    private int CountFurnitureDesigns()
    {
        var num = 0;
        for (var i = 0; i < _furnitureInventoryPanel.ComponentFurnitureInventory.SlotsCount; i++)
        {
            var slotValue = _furnitureInventoryPanel.ComponentFurnitureInventory.GetSlotValue(i);
            if (Terrain.ExtractContents(slotValue) != FurnitureBlock.Index)
            {
                continue;
            }

            var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(slotValue));
            var design = _furnitureInventoryPanel.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
            if (design != null && design.FurnitureSet == _furnitureSet)
            {
                num++;
            }
        }

        return num;
    }
}
