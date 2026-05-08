using Engine.Media;

namespace Game.Widgets;

public abstract class ButtonWidget : CanvasWidget
{
    public abstract bool IsClicked { get; set; }

    public abstract bool IsChecked { get; set; }

    public abstract bool IsAutoCheckingEnabled { get; set; }

    public abstract string Text { get; set; }

    public abstract BitmapFont Font { get; set; }

    public abstract Color Color { get; set; }
}
