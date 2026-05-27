using System.Xml.Linq;

using EntitySystem.Core;

namespace Game.Widgets;

public class CreativeInventoryWidget : CanvasWidget
{
    private const string _typeName = "CreativeInventoryWidget";

    public readonly FurnitureInventoryPanel FurnitureInventoryPanel;

    private readonly CreativeInventoryPanel _creativeInventoryPanel;

    private int _activeCategoryIndex = -1;

    private readonly List<Category> _categories = [];

    private readonly ButtonWidget _categoryButton;

    private readonly ButtonWidget _categoryLeftButton;

    private readonly ButtonWidget _categoryRightButton;

    private readonly ComponentCreativeInventory _componentCreativeInventory;

    private readonly ButtonWidget _pageDownButton;

    private readonly LabelWidget _pageLabel;

    private readonly ButtonWidget _pageUpButton;

    private readonly ContainerWidget _panelContainer;

    public CreativeInventoryWidget(Entity entity)
    {
        _componentCreativeInventory = entity.FindComponent<ComponentCreativeInventory>(true)!;
        var node = ContentManager.Get<XElement>("Widgets/CreativeInventoryWidget");
        LoadContents(this, node);
        _categoryLeftButton = Children.Find<ButtonWidget>("CategoryLeftButton")!;
        _categoryRightButton = Children.Find<ButtonWidget>("CategoryRightButton")!;
        _categoryButton = Children.Find<ButtonWidget>("CategoryButton")!;
        _pageUpButton = Children.Find<ButtonWidget>("PageUpButton")!;
        _pageDownButton = Children.Find<ButtonWidget>("PageDownButton")!;
        _pageLabel = Children.Find<LabelWidget>("PageLabel")!;
        _panelContainer = Children.Find<ContainerWidget>("PanelContainer")!;
        _creativeInventoryPanel = new CreativeInventoryPanel(this)
        {
            IsVisible = false
        };
        FurnitureInventoryPanel = new FurnitureInventoryPanel(this)
        {
            IsVisible = false
        };
        _panelContainer.Children.Add(_creativeInventoryPanel);
        _panelContainer.Children.Add(FurnitureInventoryPanel);
        foreach (var category in BlocksManager.ReadOnlyCategories)
        {
            _categories.Add(new Category
            {
                Name = category,
                Panel = _creativeInventoryPanel
            });
        }

        _categories.Add(new Category
        {
            Name = LanguageControl.Get(_typeName, 1),
            Panel = FurnitureInventoryPanel
        });
        _categories.Add(new Category
        {
            Name = LanguageControl.Get(_typeName, 2),
            Panel = _creativeInventoryPanel
        });
        foreach (var category in _categories)
        {
            if (category.Name == "Electrics")
            {
                category.Color = new Color(128, 140, 255);
            }

            if (category.Name == "Plants")
            {
                category.Color = new Color(64, 160, 64);
            }

            if (category.Name == "Weapons")
            {
                category.Color = new Color(255, 128, 112);
            }
        }
    }

    public Entity Entity => _componentCreativeInventory.Entity;

    public ButtonWidget PageDownButton => _pageDownButton;

    public ButtonWidget PageUpButton => _pageUpButton;

    public LabelWidget PageLabel => _pageLabel;

    public string GetCategoryName(int index)
    {
        return _categories[index].Name;
    }

    public override void Update()
    {
        if (_categoryLeftButton.IsClicked || Input.Left)
        {
            --_componentCreativeInventory.CategoryIndex;
        }

        if (_categoryRightButton.IsClicked || Input.Right)
        {
            ++_componentCreativeInventory.CategoryIndex;
        }

        if (_categoryButton.IsClicked)
        {
            var componentPlayer = Entity.FindComponent<ComponentPlayer>();
            if (componentPlayer != null)
            {
                DialogsManager.ShowDialog(
                    componentPlayer.GuiWidget,
                    new ListSelectionDialog(
                        string.Empty,
                        _categories,
                        56f,
                        c => new LabelWidget
                        {
                            Text = LanguageControl.Get("BlocksManager", ((Category)c).Name),
                            Color = ((Category)c).Color,
                            HorizontalAlignment = WidgetAlignment.Center,
                            VerticalAlignment = WidgetAlignment.Center
                        },
                        delegate(object c)
                        {
                            _componentCreativeInventory.CategoryIndex = _categories.IndexOf((Category)c);
                        }
                    )
                );
            }
        }

        _componentCreativeInventory.CategoryIndex =
            MathUtils.Clamp(_componentCreativeInventory.CategoryIndex, 0, _categories.Count - 1);
        _categoryButton.Text =
            LanguageControl.Get("BlocksManager", _categories[_componentCreativeInventory.CategoryIndex].Name);
        _categoryLeftButton.IsEnabled = _componentCreativeInventory.CategoryIndex > 0;
        _categoryRightButton.IsEnabled = _componentCreativeInventory.CategoryIndex < _categories.Count - 1;
        if (_componentCreativeInventory.CategoryIndex == _activeCategoryIndex)
        {
            return;
        }

        foreach (var category in _categories)
        {
            category.Panel.IsVisible = false;
        }

        _categories[_componentCreativeInventory.CategoryIndex].Panel.IsVisible = true;
        _activeCategoryIndex = _componentCreativeInventory.CategoryIndex;
    }

    public class Category
    {
        public Color Color = Color.White;

        public string Name = string.Empty;

        public required ContainerWidget Panel;
    }
}
