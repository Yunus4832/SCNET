namespace Game.Automation;

public sealed record AutomationTarget(
    string Selector,
    string Type,
    string Text,
    float X,
    float Y,
    float Width,
    float Height,
    bool Enabled,
    IReadOnlyList<string> Actions);

public sealed record AutomationUiSnapshot(
    string Screen,
    bool Transitioning,
    IReadOnlyList<string> Dialogs,
    IReadOnlyList<AutomationTarget> Targets);

public static class AutomationUiContext
{
    public static AutomationUiSnapshot Capture()
    {
        var dialogs = DialogsManager.ReadOnlyDialogs.ToArray();
        var (root, rootSelector) = GetActiveScope(dialogs);
        var targets = root is null ? [] : Enumerate(root, rootSelector).ToArray();
        return new AutomationUiSnapshot(
            ScreensManager.GetCurrentScreenName(),
            ScreensManager.IsAnimating,
            dialogs.Select(dialog => dialog.GetType().Name).ToArray(),
            targets);
    }

    public static bool TryFindTarget(string selector, out AutomationTarget target)
    {
        var (root, rootSelector) = GetActiveScope(DialogsManager.ReadOnlyDialogs.ToArray());
        var candidate = root is null
            ? null
            : Enumerate(root, rootSelector)
                .FirstOrDefault(item => string.Equals(item.Selector, selector, StringComparison.Ordinal));
        if (candidate is null)
        {
            target = null!;
            return false;
        }

        target = candidate;
        return true;
    }

    private static IEnumerable<AutomationTarget> Enumerate(Widget widget, string path)
    {
        if (IsAutomationTarget(widget) && widget is { IsVisibleGlobal: true, IsEnabledGlobal: true } &&
            widget.GlobalBounds.Max.X > widget.GlobalBounds.Min.X &&
            widget.GlobalBounds.Max.Y > widget.GlobalBounds.Min.Y)
        {
            var bounds = widget.GlobalBounds;
            var text = widget is ButtonWidget button ? button.Text : widget.Title;
            yield return new AutomationTarget(path, widget.GetType().Name, text,
                bounds.Min.X, bounds.Min.Y, bounds.Max.X - bounds.Min.X, bounds.Max.Y - bounds.Min.Y, true,
                GetActions(widget));
        }

        if (widget is not ContainerWidget container)
        {
            yield break;
        }

        for (var index = 0; index < container.Children.Count; index++)
        {
            var child = container.Children[index];
            var segment = string.IsNullOrWhiteSpace(child.Name)
                ? $"{child.GetType().Name}[{index}]"
                : child.Name;
            foreach (var item in Enumerate(child, path + "/" + segment))
            {
                yield return item;
            }
        }
    }

    private static bool IsAutomationTarget(Widget widget) =>
        widget.IsHitTestVisible &&
        (widget is ButtonWidget or ClickableWidget or ScrollPanelWidget ||
         (widget is not ContainerWidget && !string.IsNullOrWhiteSpace(widget.Name)));

    private static IReadOnlyList<string> GetActions(Widget widget) =>
        widget is ScrollPanelWidget ? ["scroll", "swipe"] : ["tap"];

    private static (Widget? Root, string Selector) GetActiveScope(IReadOnlyList<Game.Dialogs.Dialog> dialogs)
    {
        if (dialogs.Count > 0)
        {
            var dialog = dialogs[^1];
            return (dialog, "dialog/" + dialog.GetType().Name);
        }

        var screen = ScreensManager.CurrentScreen;
        return (screen, "screen/" + ScreensManager.GetCurrentScreenName());
    }
}
