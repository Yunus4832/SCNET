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
        Children.Add(MultiplayerUiStyle.CreateInsetArea());
        Children.Add(_suggestions);
        _suggestions.ItemWidgetFactory = item =>
        {
            var suggestion = (CommandSuggestion)item;
            return CreateSuggestionWidget(suggestion);
        };
        _suggestions.ItemClicked += item => SuggestionSelected?.Invoke((CommandSuggestion)item);
    }

    internal static Widget CreateSuggestionWidget(CommandSuggestion suggestion)
    {
        var panel = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Near,
            VerticalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(8, 0)
        };
        var parts = CreateTextParts(suggestion);
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            panel.Children.Add(new LabelWidget
            {
                Text = part.Text,
                Color = part.Color,
                VerticalAlignment = WidgetAlignment.Center,
                Margin = index == 0 ? Vector2.Zero : new Vector2(12, 0)
            });
        }

        return panel;
    }

    internal static IReadOnlyList<CommandSuggestionTextPart> CreateTextParts(
        CommandSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        var parts = new List<CommandSuggestionTextPart>
        {
            new(suggestion.Value, Color.White)
        };
        if (!string.IsNullOrWhiteSpace(suggestion.Description))
        {
            parts.Add(new CommandSuggestionTextPart(suggestion.Description, Color.Gray));
        }

        return parts;
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

internal sealed record CommandSuggestionTextPart(string Text, Color Color);
