namespace Game.Cameras;

public class FixedCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    public override bool UsesMovementControls => false;

    public override bool IsEntityControlEnabled => true;

    public override void Activate(Camera previousCamera)
    {
        SetupPerspectiveCamera(previousCamera.ViewPosition, previousCamera.ViewDirection, previousCamera.ViewUp);
    }

    public override void Update(float dt)
    {
    }
}
