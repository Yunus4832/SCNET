namespace Game.Dialogs;

public class Dialog : CanvasWidget
{
    public override bool IsHitTestVisible { get; set; } = true;

    protected Dialog()
    {
        Size = new Vector2(1f / 0f);
    }
}
