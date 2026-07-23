using Game.Commands;

namespace Game.Widgets;

public sealed class CommandSuggestionsWidget : CanvasWidget
{
    private readonly ListPanelWidget _suggestions = new()
    {
        Direction = LayoutDirection.Vertical,
        ItemSize = 48f
    };

    public event Action<CommandSuggestion>? SuggestionSelected;

    public bool HasSuggestions => _suggestions.Items.Count > 0;

    public CommandSuggestionsWidget()
    {
        IsVisible = false;
        Children.Add(new BevelledRectangleWidget
        {
            CenterColor = new Color(0, 0, 0, 220),
            BevelColor = Color.White,
            BevelSize = 1f
        });
        Children.Add(_suggestions);
        _suggestions.ItemWidgetFactory = item =>
        {
            var suggestion = (CommandSuggestion)item;
            return new LabelWidget
            {
                Text = $"{suggestion.Value}  <c=gray>{suggestion.Description}</c>",
                HorizontalAlignment = WidgetAlignment.Near,
                VerticalAlignment = WidgetAlignment.Center,
                Margin = new Vector2(8, 0)
            };
        };
        _suggestions.ItemClicked += item => SuggestionSelected?.Invoke((CommandSuggestion)item);
    }

    public void Refresh(string input, CommandRegistry registry, CommandPrincipal principal)
    {
        if (!input.StartsWith('/'))
        {
            Hide();
            return;
        }

        SetSuggestions(registry.Suggest(input, principal));
    }

    public void SetSuggestions(IEnumerable<CommandSuggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        _suggestions.ClearItems();
        foreach (var suggestion in suggestions)
        {
            _suggestions.AddItem(suggestion);
        }

        IsVisible = _suggestions.Items.Count > 0;
    }

    public void Hide()
    {
        _suggestions.ClearItems();
        IsVisible = false;
    }
}
