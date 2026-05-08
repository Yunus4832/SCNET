using Engine.Graphics;

namespace Game.Widgets;

public class ClearWidget : Widget
{
    public override bool IsHitTestVisible { get; set; } = false;

    public Color Color { get; set; } = Color.Black;

    public float Depth { get; set; } = 1f;

    public int Stencil { get; set; } = 0;

    public bool ClearColor { get; set; } = true;

    public bool ClearDepth { get; set; } = true;

    public bool ClearStencil { get; set; } = true;

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
    }

    public override void Draw(DrawContext dc)
    {
        Display.Clear(
            ClearColor
                ? new Vector4?(new Vector4(Color))
                : null, ClearDepth
                ? new float?(Depth)
                : null,
            ClearStencil
                ? new int?(Stencil)
                : null
        );
    }
}
