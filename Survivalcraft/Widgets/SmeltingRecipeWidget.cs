using System.Xml.Linq;

namespace Game.Widgets;

public class SmeltingRecipeWidget : CanvasWidget
{
    private readonly LabelWidget _descriptionWidget;

    private bool _dirty = true;

    private readonly FireWidget _fireWidget;

    private readonly GridPanelWidget _gridWidget;

    private readonly LabelWidget _nameWidget;

    private CraftingRecipe? _recipe;

    private readonly CraftingRecipeSlotWidget _resultWidget;

    public string NameSuffix
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _dirty = true;
        }
    } = string.Empty;

    public CraftingRecipe? Recipe
    {
        get => _recipe;
        set
        {
            if (value == _recipe)
            {
                return;
            }

            _recipe = value;
            _dirty = true;
        }
    }


    public SmeltingRecipeWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/SmeltingRecipe");
        LoadContents(this, node);
        _nameWidget = Children.Find<LabelWidget>("SmeltingRecipeWidget.Name")!;
        _descriptionWidget = Children.Find<LabelWidget>("SmeltingRecipeWidget.Description")!;
        _gridWidget = Children.Find<GridPanelWidget>("SmeltingRecipeWidget.Ingredients")!;
        _fireWidget = Children.Find<FireWidget>("SmeltingRecipeWidget.Fire")!;
        _resultWidget = Children.Find<CraftingRecipeSlotWidget>("SmeltingRecipeWidget.Result")!;
        for (var i = 0; i < _gridWidget.RowsCount; i++)
        {
            for (var j = 0; j < _gridWidget.ColumnsCount; j++)
            {
                var widget = new CraftingRecipeSlotWidget();
                _gridWidget.Children.Add(widget);
                _gridWidget.SetWidgetCell(widget, new Point2(j, i));
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (_dirty)
        {
            UpdateWidgets();
        }

        base.MeasureOverride(parentAvailableSize);
    }

    public void UpdateWidgets()
    {
        _dirty = false;
        if (_recipe != null)
        {
            var block = BlocksManager.Blocks[Terrain.ExtractContents(_recipe.ResultValue)];
            _nameWidget.Text = block.GetDisplayName(null, _recipe.ResultValue) +
                               (!string.IsNullOrEmpty(NameSuffix) ? NameSuffix : string.Empty);
            _descriptionWidget.Text = _recipe.Description;
            _nameWidget.IsVisible = true;
            _descriptionWidget.IsVisible = true;
            foreach (var widget in _gridWidget.Children)
            {
                var child = (CraftingRecipeSlotWidget)widget;
                var widgetCell = _gridWidget.GetWidgetCell(child);
                child.SetIngredient(_recipe.Ingredients[widgetCell.X + widgetCell.Y * 3]);
            }

            _resultWidget.SetResult(_recipe.ResultValue, _recipe.ResultCount);
            _fireWidget.ParticlesPerSecond = 40f;
        }
        else
        {
            _nameWidget.IsVisible = false;
            _descriptionWidget.IsVisible = false;
            foreach (var widget in _gridWidget.Children)
            {
                var child2 = (CraftingRecipeSlotWidget)widget;
                child2.SetIngredient(string.Empty);
            }

            _resultWidget.SetResult(0, 0);
            _fireWidget.ParticlesPerSecond = 0f;
        }
    }
}
