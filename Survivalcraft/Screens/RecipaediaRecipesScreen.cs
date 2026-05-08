using System.Xml.Linq;

namespace Game.Screens;

public class RecipaediaRecipesScreen : Screen
{
    private readonly List<CraftingRecipe> _craftingRecipes = [];

    private readonly CraftingRecipeWidget _craftingRecipeWidget;

    private readonly ButtonWidget _nextRecipeButton;

    private readonly ButtonWidget _prevRecipeButton;

    private int _recipeIndex;

    private readonly SmeltingRecipeWidget _smeltingRecipeWidget;

    public RecipaediaRecipesScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/RecipaediaRecipesScreen");
        LoadContents(this, node);
        _craftingRecipeWidget = Children.Find<CraftingRecipeWidget>("CraftingRecipe")!;
        _smeltingRecipeWidget = Children.Find<SmeltingRecipeWidget>("SmeltingRecipe")!;
        _prevRecipeButton = Children.Find<ButtonWidget>("PreviousRecipe")!;
        _nextRecipeButton = Children.Find<ButtonWidget>("NextRecipe")!;
    }

    public override void Enter(object[] parameters)
    {
        var value = (int)parameters[0];
        _craftingRecipes.Clear();
        _craftingRecipes.AddRange(
            CraftingRecipesManager.ReadonlyRecipes.Where(r => r.ResultValue == value && r.ResultValue != 0));
        _recipeIndex = 0;
    }

    public override void Update()
    {
        if (_recipeIndex < _craftingRecipes.Count)
        {
            var craftingRecipe = _craftingRecipes[_recipeIndex];
            if (craftingRecipe.RequiredHeatLevel == 0f)
            {
                _craftingRecipeWidget.Recipe = craftingRecipe;
                _craftingRecipeWidget.NameSuffix = $" (recipe #{_recipeIndex + 1})";
                _craftingRecipeWidget.IsVisible = true;
                _smeltingRecipeWidget.IsVisible = false;
            }
            else
            {
                _smeltingRecipeWidget.Recipe = craftingRecipe;
                _smeltingRecipeWidget.NameSuffix = $" (recipe #{_recipeIndex + 1})";
                _smeltingRecipeWidget.IsVisible = true;
                _craftingRecipeWidget.IsVisible = false;
            }
        }

        _prevRecipeButton.IsEnabled = _recipeIndex > 0;
        _nextRecipeButton.IsEnabled = _recipeIndex < _craftingRecipes.Count - 1;
        if (_prevRecipeButton.IsClicked)
        {
            _recipeIndex = MathUtils.Max(_recipeIndex - 1, 0);
        }

        if (_nextRecipeButton.IsClicked)
        {
            _recipeIndex = MathUtils.Min(_recipeIndex + 1, _craftingRecipes.Count - 1);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
