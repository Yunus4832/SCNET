namespace Game.Widgets;

[Obsolete($"Use {nameof(RichTextWidget)} instead.")]
public class AutoCanvasWidget : RichTextWidget
{
    [Obsolete($"Use {nameof(Text)} instead.")]
    public string ContentText
    {
        get => Text;
        set => Text = value;
    }
}
