namespace Game.Cameras;

public class TppCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    private Vector3 _position;

    public override bool UsesMovementControls => false;

    public override bool IsEntityControlEnabled => true;

    public override void Activate(Camera previousCamera)
    {
        _position = previousCamera.ViewPosition;
        SetupPerspectiveCamera(_position, previousCamera.ViewDirection, previousCamera.ViewUp);
    }

    public override void Update(float dt)
    {
        if (GameWidget.Target == null)
        {
            return;
        }

        var matrix = Matrix.CreateFromQuaternion(GameWidget.Target.ComponentCreaturePose.EyeRotation);
        matrix.Translation = GameWidget.Target.ComponentBody.Position +
                             0.5f * GameWidget.Target.ComponentBody.BoxSize.Y * Vector3.UnitY;
        var v = -2.25f * matrix.Forward + 1.75f * matrix.Up;
        var vector = matrix.Translation + v;
        if (Vector3.Distance(vector, _position) < 10f)
        {
            var v2 = vector - _position;
            var s = 3f * dt;
            _position += s * v2;
        }
        else
        {
            _position = vector;
        }

        var cameraOffset = _position - matrix.Translation;
        float? nearestObstacleDistance = null;
        var cameraRight = Vector3.Normalize(Vector3.Cross(cameraOffset, Vector3.UnitY));
        var v3 = Vector3.Normalize(Vector3.Cross(cameraOffset, cameraRight));
        for (var i = 0; i <= 0; i++)
        {
            for (var j = 0; j <= 0; j++)
            {
                var v4 = 0.5f * (cameraRight * i + v3 * j);
                var rayStart = matrix.Translation + v4;
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
            ? matrix.Translation + cameraOffset
            : matrix.Translation +
              Vector3.Normalize(cameraOffset) * MathUtils.Max(nearestObstacleDistance.Value - 0.5f, 0.2f);
        SetupPerspectiveCamera(cameraPosition, matrix.Translation - cameraPosition, Vector3.UnitY);
    }
}
