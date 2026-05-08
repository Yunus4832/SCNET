using System.Xml.Linq;

namespace Game.Widgets;

public class CraftingRecipeSlotWidget : CanvasWidget
{
    private readonly BlockIconWidget _blockIconWidget;

    private string _ingredient = string.Empty;

    private readonly LabelWidget _labelWidget;

    private int _resultCount;

    private int _resultValue;

    public CraftingRecipeSlotWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/CraftingRecipeSlot");
        LoadContents(this, node);
        _blockIconWidget = Children.Find<BlockIconWidget>("CraftingRecipeSlotWidget.Icon")!;
        _labelWidget = Children.Find<LabelWidget>("CraftingRecipeSlotWidget.Count")!;
    }

    public void SetIngredient(string ingredient)
    {
        _ingredient = ingredient;
        _resultValue = 0;
        _resultCount = 0;
    }

    public void SetResult(int value, int count)
    {
        _resultValue = value;
        _resultCount = count;
        _ingredient = string.Empty;
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        _blockIconWidget.IsVisible = false;
        _labelWidget.IsVisible = false;
        if (!string.IsNullOrEmpty(_ingredient))
        {
            CraftingRecipesManager.DecodeIngredient(_ingredient, out var craftingId, out var data);
            var array = BlocksManager.FindBlocksByCraftingId(craftingId);
            if (array.Length != 0)
            {
                var block = array[(int)(1.0 * Time.RealTime) % array.Length];
                _blockIconWidget.Value =
                    Terrain.MakeBlockValue(block.BlockIndex, 0, data ?? 4);
                _blockIconWidget.Light = 15;
                _blockIconWidget.IsVisible = true;
            }
        }
        else if (_resultValue != 0)
        {
            _blockIconWidget.Value = _resultValue;
            _blockIconWidget.Light = 15;
            _labelWidget.Text = _resultCount.ToString();
            _blockIconWidget.IsVisible = true;
            _labelWidget.IsVisible = true;
        }

        base.MeasureOverride(parentAvailableSize);
    }
}
