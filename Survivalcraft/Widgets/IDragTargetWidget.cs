namespace Game.Widgets;

public interface IDragTargetWidget
{
    void DragOut(Widget dragWidget, object data);

    void DragIn(Widget dragWidget, object data);

    void DragOver(Widget dragWidget, object data);

    void DragDrop(Widget dragWidget, object data);
}

public sealed class DragDataDefault
{
    public static readonly DragDataDefault Default = new();

    private DragDataDefault()
    {
    }
};
