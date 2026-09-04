namespace Game.Cameras;

public class OrbitCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    private Vector2 _angles = new(0f, MathUtils.DegToRad(30f));

    private float _distance = 6f;

    private Vector3 _position;

    public override bool UsesMovementControls => true;

    public override bool IsEntityControlEnabled => true;

    public override void Activate(Camera previousCamera)
    {
        SetupPerspectiveCamera(previousCamera.ViewPosition, previousCamera.ViewDirection, previousCamera.ViewUp);
    }

    public override void Update(float dt)
    {
        var componentPlayer = GameWidget.PlayerData.ComponentPlayer;
        if (componentPlayer == null || GameWidget.Target == null)
        {
            return;
        }

        var componentInput = componentPlayer.ComponentInput;
        var cameraSneakMove = componentInput.PlayerInput.CameraSneakMove;
        var cameraLook = componentInput.PlayerInput.CameraLook;
        _angles.X = MathUtils.NormalizeAngle(_angles.X + 4f * cameraLook.X * dt + 0.5f * cameraSneakMove.X * dt);
        _angles.Y = MathUtils.Clamp(MathUtils.NormalizeAngle(_angles.Y + 4f * cameraLook.Y * dt),
            MathUtils.DegToRad(-20f), MathUtils.DegToRad(70f));
        _distance = MathUtils.Clamp(_distance - 10f * cameraSneakMove.Z * dt, 2f, 16f);
        var v = Vector3.Transform(new Vector3(_distance, 0f, 0f),
            Matrix.CreateFromYawPitchRoll(_angles.X, 0f, _angles.Y));
        var vector = GameWidget.Target.ComponentBody.BoundingBox.Center();
        var vector2 = vector + v;
        if (Vector3.Distance(vector2, _position) < 10f)
        {
            var v2 = vector2 - _position;
            var s = MathUtils.Saturate(10f * dt);
            _position += s * v2;
        }
        else
        {
            _position = vector2;
        }

        var vector3 = _position - vector;
        float? num = null;
        var vector4 = Vector3.Normalize(Vector3.Cross(vector3, Vector3.UnitY));
        var v3 = Vector3.Normalize(Vector3.Cross(vector3, vector4));
        for (var i = 0; i <= 0; i++)
        {
            for (var j = 0; j <= 0; j++)
            {
                var v4 = 0.5f * (vector4 * i + v3 * j);
                var vector5 = vector + v4;
                var end = vector5 + vector3 + Vector3.Normalize(vector3) * 0.5f;
                var terrainRaycastResult = GameWidget.SubsystemGameWidgets.SubsystemTerrain.Raycast(vector5, end, false,
                    true, (value, _) => !BlocksManager.Blocks[Terrain.ExtractContents(value)].Transparent);
                if (terrainRaycastResult.HasValue)
                {
                    num = num.HasValue
                        ? MathUtils.Min(num.Value, terrainRaycastResult.Value.Distance)
                        : terrainRaycastResult.Value.Distance;
                }
            }
        }

        var vector6 = !num.HasValue
            ? vector + vector3
            : vector + Vector3.Normalize(vector3) * MathUtils.Max(num.Value - 0.5f, 0.2f);
        SetupPerspectiveCamera(vector6, vector - vector6, Vector3.UnitY);
    }
}
