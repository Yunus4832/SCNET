using System.Xml.Linq;

using Game.Network;
using Game.Network.Packages;

namespace Game.Widgets;

public class CrossbowWidget : CanvasWidget
{
    private const string _typeName = "CrossbowWidget";

    private float? _dragStartOffset;

    private readonly LabelWidget _instructionsLabel;

    private readonly IInventory _inventory;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly InventorySlotWidget _inventorySlotWidget;

    private readonly Random _random = new();

    private readonly int _slotIndex;

    public CrossbowWidget(IInventory inventory, int slotIndex)
    {
        _inventory = inventory;
        _slotIndex = slotIndex;
        var node = ContentManager.Get<XElement>("Widgets/CrossbowWidget");
        LoadContents(this, node);
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _inventorySlotWidget = Children.Find<InventorySlotWidget>("InventorySlot")!;
        _instructionsLabel = Children.Find<LabelWidget>("InstructionsLabel")!;
        for (var i = 0; i < _inventoryGrid.RowsCount; i++)
        for (var j = 0; j < _inventoryGrid.ColumnsCount; j++)
        {
            var widget = new InventorySlotWidget();
            _inventoryGrid.Children.Add(widget);
            _inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
        }

        var num = 10;
        foreach (var child in _inventoryGrid.Children)
        {
            (child as InventorySlotWidget)?.AssignInventorySlot(inventory, num++);
        }

        _inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
        _inventorySlotWidget.CustomViewMatrix =
            Matrix.CreateLookAt(new Vector3(0f, 1f, 0.2f), new Vector3(0f, 0f, 0.2f), -Vector3.UnitZ);
    }

    public override void Update()
    {
        var slotValue = _inventory.GetSlotValue(_slotIndex);
        var slotCount = _inventory.GetSlotCount(_slotIndex);
        var num = Terrain.ExtractContents(slotValue);
        var data = Terrain.ExtractData(slotValue);
        var draw = CrossbowBlock.GetDraw(data);
        var arrowType = CrossbowBlock.GetArrowType(data);
        if (num == 200 && slotCount > 0)
        {
            if (draw < 15)
            {
                _instructionsLabel.Text = LanguageControl.Get(_typeName, 0);
            }
            else
            {
                _instructionsLabel.Text =
                    !arrowType.HasValue ? LanguageControl.Get(_typeName, 1) : LanguageControl.Get(_typeName, 2);
            }

            if ((draw < 15 || !arrowType.HasValue) && Input.Tap.HasValue &&
                HitTestGlobal(Input.Tap.Value) == _inventorySlotWidget)
            {
                if (Input.Press.HasValue)
                {
                    var vector = _inventorySlotWidget.ScreenToWidget(Input.Press.Value);
                    var num2 = vector.Y - DrawToPosition(draw);
                    if (MathUtils.Abs(vector.X - _inventorySlotWidget.ActualSize.X / 2f) < 25f &&
                        MathUtils.Abs(num2) < 25f)
                    {
                        _dragStartOffset = num2;
                    }
                }
            }

            if (!_dragStartOffset.HasValue)
            {
                return;
            }

            if (Input.Press.HasValue)
            {
                var num3 = PositionToDraw(_inventorySlotWidget.ScreenToWidget(Input.Press.Value).Y -
                                          _dragStartOffset.Value);
                SetDraw(num3);
                if (draw <= 9 && num3 > 9)
                {
                    AudioManager.PlaySound("Audio/CrossbowDraw", 1f, _random.Float(-0.2f, 0.2f), 0f);
                }
            }
            else
            {
                _dragStartOffset = null;
                if (draw == 15)
                {
                    CommonLib.Net.QueuePackage(new BlockEditPackage(_inventory, _slotIndex,
                        BlockEditPackage.EventType.CrossbowPull));

                    AudioManager.PlaySound("Audio/UI/ItemMoved", 1f, 0f, 0f);
                    return;
                }

                SetDraw(0);
                AudioManager.PlaySound("Audio/CrossbowBoing", MathUtils.Saturate((draw - 3) / 10f),
                    _random.Float(-0.1f, 0.1f), 0f);
            }
        }
        else
        {
            ParentWidget?.Children.Remove(this);
        }
    }

    public void SetDraw(int draw)
    {
        var data = Terrain.ExtractData(_inventory.GetSlotValue(_slotIndex));
        var value = Terrain.MakeBlockValue(200, 0, CrossbowBlock.SetDraw(data, draw));

        _inventory.RemoveSlotItems(_slotIndex, 1);
        _inventory.AddSlotItems(_slotIndex, value, 1);
    }

    public static float DrawToPosition(int draw)
    {
        return draw * 5.4f + 85f;
    }

    public static int PositionToDraw(float position)
    {
        return (int)MathUtils.Clamp(MathUtils.Round((position - 85f) / 5.4f), 0f, 15f);
    }
}
