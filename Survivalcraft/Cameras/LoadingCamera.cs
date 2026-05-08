namespace Game.Cameras;

public class LoadingCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    public override bool UsesMovementControls => false;

    public override bool IsEntityControlEnabled => false;

    public override void Activate(Camera previousCamera)
    {
        SetupPerspectiveCamera(previousCamera.ViewPosition, previousCamera.ViewDirection, previousCamera.ViewUp);
    }

    public override void Update(float dt)
    {
        SetupPerspectiveCamera(GameWidget.PlayerData.SpawnPosition, Vector3.UnitX, Vector3.UnitY);
    }
}
