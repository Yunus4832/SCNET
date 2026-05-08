namespace Game.Cameras;

public class StraightFlightCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    private Vector3 _position;

    public override bool UsesMovementControls => false;

    public override bool IsEntityControlEnabled => false;

    public override void Activate(Camera previousCamera)
    {
        _position = previousCamera.ViewPosition;
        SetupPerspectiveCamera(_position, previousCamera.ViewDirection, previousCamera.ViewUp);
    }

    public override void Update(float dt)
    {
        var vector = 10f * (Vector3.UnitX +
                            (float)MathUtils.Sin(0.20000000298023224 * Time.FrameStartTime) * Vector3.UnitZ);
        _position.Y = 120f;
        _position += vector * dt;
        SetupPerspectiveCamera(_position, vector, Vector3.UnitY);
    }
}
