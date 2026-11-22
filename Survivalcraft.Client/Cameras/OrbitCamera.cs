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
        var componentCharacter = GameWidget.CharacterData.ComponentCharacter;
        if (componentCharacter == null || GameWidget.Target == null)
        {
            return;
        }

        var componentInput = componentCharacter.Entity.FindComponent<ComponentInput>(true)!;
        var cameraSneakMove = componentInput.PlayerInput.CameraSneakMove;
        var cameraLook = componentInput.PlayerInput.CameraLook;
        _angles.X = MathUtils.NormalizeAngle(_angles.X + 4f * cameraLook.X * dt + 0.5f * cameraSneakMove.X * dt);
        _angles.Y = MathUtils.Clamp(
            MathUtils.NormalizeAngle(_angles.Y + 4f * cameraLook.Y * dt),
            MathUtils.DegToRad(-20f),
            MathUtils.DegToRad(70f)
        );
        _distance = MathUtils.Clamp(_distance - 10f * cameraSneakMove.Z * dt, 2f, 16f);
        var v = Vector3.Transform(
            new Vector3(_distance, 0f, 0f),
            Matrix.CreateFromYawPitchRoll(_angles.X, 0f, _angles.Y)
        );
        var vector = GameWidget.Target.ComponentBody.BoundingBox.Center();
        var desiredPosition = vector + v;
        if (Vector3.Distance(desiredPosition, _position) < 10f)
        {
            var v2 = desiredPosition - _position;
            var s = MathUtils.Saturate(10f * dt);
            _position += s * v2;
        }
        else
        {
            _position = desiredPosition;
        }

        var cameraOffset = _position - vector;
        float? nearestObstacleDistance = null;
        var cameraRight = Vector3.Normalize(Vector3.Cross(cameraOffset, Vector3.UnitY));
        var v3 = Vector3.Normalize(Vector3.Cross(cameraOffset, cameraRight));
        for (var i = 0; i <= 0; i++)
        {
            for (var j = 0; j <= 0; j++)
            {
                var v4 = 0.5f * (cameraRight * i + v3 * j);
                var rayStart = vector + v4;
                var end = rayStart + cameraOffset + Vector3.Normalize(cameraOffset) * 0.5f;
                var terrainRaycastResult = GameWidget.SubsystemGameWidgets.SubsystemTerrain.Raycast(
                    rayStart,
                    end,
                    false,
                    true,
                    (value, _) => !GameWidget.SubsystemGameWidgets.SubsystemTerrain.Catalog
                        .Blocks[Terrain.ExtractContents(value)]
                        .Transparent
                );
                if (terrainRaycastResult.HasValue)
                {
                    nearestObstacleDistance = nearestObstacleDistance.HasValue
                        ? MathUtils.Min(nearestObstacleDistance.Value, terrainRaycastResult.Value.Distance)
                        : terrainRaycastResult.Value.Distance;
                }
            }
        }

        var cameraPosition = !nearestObstacleDistance.HasValue
            ? vector + cameraOffset
            : vector + Vector3.Normalize(cameraOffset) * MathUtils.Max(nearestObstacleDistance.Value - 0.5f, 0.2f);
        SetupPerspectiveCamera(cameraPosition, vector - cameraPosition, Vector3.UnitY);
    }
}
