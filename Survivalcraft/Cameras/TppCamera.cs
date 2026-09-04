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

        var matrix = Matrix.CreateFromQuaternion(GameWidget.Target.ComponentCreatureModel.EyeRotation);
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

        var vector2 = _position - matrix.Translation;
        float? num = null;
        var vector3 = Vector3.Normalize(Vector3.Cross(vector2, Vector3.UnitY));
        var v3 = Vector3.Normalize(Vector3.Cross(vector2, vector3));
        for (var i = 0; i <= 0; i++)
        {
            for (var j = 0; j <= 0; j++)
            {
                var v4 = 0.5f * (vector3 * i + v3 * j);
                var vector4 = matrix.Translation + v4;
                var end = vector4 + vector2 + Vector3.Normalize(vector2) * 0.5f;
                var terrainRaycastResult = GameWidget.SubsystemGameWidgets.SubsystemTerrain.Raycast(vector4, end, false,
                    true, (value, _) => !BlocksManager.Blocks[Terrain.ExtractContents(value)].Transparent);
                if (terrainRaycastResult.HasValue)
                {
                    num = num.HasValue
                        ? MathUtils.Min(num.Value, terrainRaycastResult.Value.Distance)
                        : terrainRaycastResult.Value.Distance;
                }
            }
        }

        var vector5 = !num.HasValue
            ? matrix.Translation + vector2
            : matrix.Translation + Vector3.Normalize(vector2) * MathUtils.Max(num.Value - 0.5f, 0.2f);
        SetupPerspectiveCamera(vector5, matrix.Translation - vector5, Vector3.UnitY);
    }
}
