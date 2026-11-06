namespace Game.Widgets;

public class FireWidget : CanvasWidget
{
    private readonly ScreenSpaceFireRenderer _fireRenderer = new(100);

    public FireWidget()
    {
        ClampToBounds = true;
    }

    public float ParticlesPerSecond
    {
        get => _fireRenderer.ParticlesPerSecond;
        set => _fireRenderer.ParticlesPerSecond = value;
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        base.MeasureOverride(parentAvailableSize);
    }

    public override void Draw(DrawContext dc)
    {
        _fireRenderer.Draw(dc.PrimitivesRenderer2D, 0f, GlobalTransform, GlobalColorTransform);
    }

    public override void Update()
    {
        var dt = MathUtils.Clamp(Time.FrameDuration, 0f, 0.1f);
        _fireRenderer.Origin = new Vector2(0f, ActualSize.Y);
        _fireRenderer.CutoffPosition = float.NegativeInfinity;
        _fireRenderer.ParticleSize = 32f;
        _fireRenderer.ParticleSpeed = 32f;
        _fireRenderer.Width = ActualSize.X;
        _fireRenderer.MinTimeToLive = 0.5f;
        _fireRenderer.MaxTimeToLive = 2f;
        _fireRenderer.ParticleAnimationPeriod = 1.25f;
        _fireRenderer.Update(dt);
    }
}
