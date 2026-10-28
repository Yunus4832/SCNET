namespace Game.Widgets.Services;

public abstract class ServicePanelWidget : CanvasWidget
{
    private object? _selection;

    private float _scroll;

    public void Show()
    {
        IsVisible = true;
        Activate();
        var list = Children.Find<ListPanelWidget>("SourceList", false) ??
                   Children.Find<ListPanelWidget>("Services", false);
        if (list is not null)
        {
            list.SelectedItem = list.Items.FirstOrDefault(item => Equals(item, _selection));
            list.ScrollPosition = _scroll;
        }

        Children.Find<ActionPanelWidget>("Actions")!.ShowPrimaryItems();
    }

    public void Hide()
    {
        var list = Children.Find<ListPanelWidget>("SourceList", false) ??
                   Children.Find<ListPanelWidget>("Services", false);
        _selection = list?.SelectedItem;
        _scroll = list?.ScrollPosition ?? 0;
        Deactivate();
        IsVisible = false;
    }

    public abstract void Activate();

    public abstract void Deactivate();
}
