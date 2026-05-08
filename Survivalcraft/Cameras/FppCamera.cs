namespace Game.Cameras;

public class FppCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    public override bool UsesMovementControls => false;

    public override bool IsEntityControlEnabled => true;

    public override void Activate(Camera previousCamera)
    {
        SetupPerspectiveCamera(previousCamera.ViewPosition, previousCamera.ViewDirection, previousCamera.ViewUp);
    }

    public override void Update(float dt)
    {
        if (GameWidget.Target == null)
        {
            return;
        }

        if (!Eye.HasValue)
        {
            var matrix = Matrix.CreateFromQuaternion(GameWidget.Target.ComponentCreatureModel.EyeRotation);
            matrix.Translation = GameWidget.Target.ComponentCreatureModel.EyePosition;
            SetupPerspectiveCamera(matrix.Translation, matrix.Forward, matrix.Up);
            return;
        }

        var translation = VrManager.HmdMatrix.Translation;
        var position = GameWidget.Target.ComponentBody.Position;
        var y = position.Y + MathUtils.Clamp(translation.Y, 0.2f, GameWidget.Target.ComponentBody.BoxSize.Y - 0.1f);
        var hmdMatrixYpr = VrManager.HmdMatrixYpr;
        var vector = GameWidget.Target.ComponentCreatureModel.EyeRotation.ToYawPitchRoll();
        var radians = vector.X - hmdMatrixYpr.X;
        var identity = Matrix.Identity;
        identity.Translation = new Vector3(position.X, y, position.Z);
        identity.OrientationMatrix = VrManager.HmdMatrix * Matrix.CreateRotationY(radians);
        identity.OrientationMatrix *= Matrix.CreateFromAxisAngle(identity.OrientationMatrix.Forward, vector.Z);
        SetupPerspectiveCamera(identity.Translation, identity.Forward, identity.Up);
    }
}
