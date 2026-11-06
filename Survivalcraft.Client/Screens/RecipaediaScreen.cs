using System.Xml.Linq;

using Game.Application;

namespace Game.Screens;

public class RecipaediaScreen : Screen
{
    private readonly ListPanelWidget _blocksList;

    private readonly List<string> _categories = [];

    private int _categoryIndex;

    private readonly LabelWidget _categoryLabel;

    private readonly ButtonWidget _detailsButton;

    private int _listCategoryIndex = -1;

    private readonly ButtonWidget _nextCategoryButton;

    private readonly ButtonWidget _prevCategoryButton;

    private Screen? _previousScreen;

    private readonly ButtonWidget _recipesButton;

    public RecipaediaScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/RecipaediaScreen");
        LoadContents(this, node);
        _blocksList = Children.Find<ListPanelWidget>("BlocksList")!;
        _categoryLabel = Children.Find<LabelWidget>("Category")!;
        _prevCategoryButton = Children.Find<ButtonWidget>("PreviousCategory")!;
        _nextCategoryButton = Children.Find<ButtonWidget>("NextCategory")!;
        _detailsButton = Children.Find<ButtonWidget>("DetailsButton")!;
        _recipesButton = Children.Find<ButtonWidget>("RecipesButton")!;
        _categories.Add(string.Empty);
        _categories.AddRange(ClientApplication.Content.PreviewCatalog.ReadOnlyCategories);
        _blocksList.ItemWidgetFactory = delegate(object item)
        {
            var value = (int)item;
            var num = Terrain.ExtractContents(value);
            var block = ClientApplication.Content.PreviewCatalog.Get(num);
            var node2 = ContentManager.Get<XElement>("Widgets/RecipaediaItem");
            var obj = (ContainerWidget)LoadWidget(this, node2, null);
            obj.Children.Find<BlockIconWidget>("RecipaediaItem.Icon")!.Catalog =
                ClientApplication.Content.PreviewCatalog;
            obj.Children.Find<BlockIconWidget>("RecipaediaItem.Icon")!.Value = value;
            obj.Children.Find<LabelWidget>("RecipaediaItem.Text")!.Text = block.GetDisplayName(null, value);
            obj.Children.Find<LabelWidget>("RecipaediaItem.Details")!.Text = block.GetDescription(value);
            return obj;
        };
        _blocksList.ItemClicked += delegate(object item)
        {
            if (_blocksList.SelectedItem == item && item is int)
            {
                ScreensManager.SwitchScreen("RecipaediaDescription", item, _blocksList.Items.Cast<int>().ToList());
            }
        };
    }

    public override void Enter(object[] parameters)
    {
        if (ScreensManager.PreviousScreen != ScreensManager.FindScreen<Screen>("RecipaediaRecipes") &&
            ScreensManager.PreviousScreen != ScreensManager.FindScreen<Screen>("RecipaediaDescription"))
        {
            _previousScreen = ScreensManager.PreviousScreen;
        }
    }

    public override void Update()
    {
        if (_listCategoryIndex != _categoryIndex)
        {
            PopulateBlocksList();
        }

        var arg = string.IsNullOrEmpty(_categories[_categoryIndex])
            ? Localization.ClientLanguage.Catalog.Get("BlocksManager", "All Blocks")
            : Localization.ClientLanguage.Catalog.Get("BlocksManager", _categories[_categoryIndex]);
        _categoryLabel.Text = $"{arg} ({_blocksList.Items.Count})";
        _prevCategoryButton.IsEnabled = _categoryIndex > 0;
        _nextCategoryButton.IsEnabled = _categoryIndex < _categories.Count - 1;
        int? value = null;
        var num = 0;
        if (_blocksList.SelectedItem is int item)
        {
            value = item;
            num = ClientApplication.Content.PreviewCatalog.Rules.Recipes.ReadonlyRecipes.Count(r =>
                r.ResultValue == value
            );
        }

        if (num > 0)
        {
            _recipesButton.Text =
                $"{num} {(num == 1 ? Localization.ClientLanguage.Catalog.Get(GetType().Name, 1) : Localization.ClientLanguage.Catalog.Get(GetType().Name, 2))}";
            _recipesButton.IsEnabled = true;
        }
        else
        {
            _recipesButton.Text = Localization.ClientLanguage.Catalog.Get(GetType().Name, 3);
            _recipesButton.IsEnabled = false;
        }

        _detailsButton.IsEnabled = value.HasValue;
        if (_prevCategoryButton.IsClicked || Input.Left)
        {
            _categoryIndex = MathUtils.Max(_categoryIndex - 1, 0);
        }

        if (_nextCategoryButton.IsClicked || Input.Right)
        {
            _categoryIndex = MathUtils.Min(_categoryIndex + 1, _categories.Count - 1);
        }

        if (value.HasValue && _detailsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("RecipaediaDescription", value.Value, _blocksList.Items.Cast<int>().ToList());
        }

        if (value.HasValue && _recipesButton.IsClicked)
        {
            ScreensManager.SwitchScreen("RecipaediaRecipes", value.Value);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_previousScreen);
        }
    }

    private void PopulateBlocksList()
    {
        _listCategoryIndex = _categoryIndex;
        var text = _categories[_categoryIndex];
        _blocksList.ScrollPosition = 0f;
        _blocksList.ClearItems();

        var catalog = ClientApplication.Content.PreviewCatalog;
        foreach (var value in catalog.GetCreativeValues())
        {
            if (string.IsNullOrEmpty(text) || catalog.Get(Terrain.ExtractContents(value)).GetCategory(value) == text)
            {
                _blocksList.AddItem(value);
            }
        }
    }
}
