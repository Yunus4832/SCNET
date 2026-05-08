namespace Game.Cameras;

public class DeathCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    private Vector3? _bestPosition;

    private Vector3 _position;

    private float _vrDeltaYaw;

    public override bool UsesMovementControls => false;

    public override bool IsEntityControlEnabled => false;

    public override void Activate(Camera previousCamera)
    {
        _position = previousCamera.ViewPosition;
        var vector = GameWidget.Target?.ComponentBody.BoundingBox.Center() ?? _position;
        _bestPosition = FindBestCameraPosition(vector, 6f);
        SetupPerspectiveCamera(_position, vector - _position, Vector3.UnitY);
        if (GameWidget.Target is not ComponentPlayer { ComponentInput.IsControlledByVr: true } ||
            !_bestPosition.HasValue)
        {
            return;
        }

        var vector2 = Matrix.CreateWorld(Vector3.Zero, vector - _bestPosition.Value, Vector3.UnitY)
            .ToYawPitchRoll();
        _vrDeltaYaw = vector2.X - VrManager.HmdMatrixYpr.X;
    }

    public override void Update(float dt)
    {
        var v = GameWidget.Target?.ComponentBody.BoundingBox.Center() ?? _position;
        if (_bestPosition.HasValue)
        {
            if (Vector3.Distance(_bestPosition.Value, _position) > 20f)
            {
                _position = _bestPosition.Value;
            }

            _position += 1.5f * dt * (_bestPosition.Value - _position);
        }

        if (!Eye.HasValue)
        {
            SetupPerspectiveCamera(_position, v - _position, Vector3.UnitY);
            return;
        }

        var identity = Matrix.Identity;
        identity.Translation = _position;
        identity.OrientationMatrix = VrManager.HmdMatrix * Matrix.CreateRotationY(_vrDeltaYaw);
        SetupPerspectiveCamera(identity.Translation, identity.Forward, identity.Up);
    }

    public Vector3 FindBestCameraPosition(Vector3 targetPosition, float distance)
    {
        Vector3? vector = null;
        for (var i = 0; i < 36; i++)
        {
            var x = 1f + (float)Math.PI * 2f * i / 36f;
            var v2 = Vector3.Normalize(new Vector3(MathUtils.Sin(x), 0.5f, MathUtils.Cos(x)));
            var vector2 = targetPosition + v2 * distance;
            var terrainRaycastResult = GameWidget.SubsystemGameWidgets.SubsystemTerrain.Raycast(targetPosition, vector2,
                false, true, (v, _) => !BlocksManager.Blocks[Terrain.ExtractContents(v)].Transparent);
            Vector3 zero;
            if (terrainRaycastResult.HasValue)
            {
                var cellFace = terrainRaycastResult.Value.CellFace;
                zero = new Vector3(cellFace.X + 0.5f, cellFace.Y + 0.5f, cellFace.Z + 0.5f) - 1f * v2;
            }
            else
            {
                zero = vector2;
            }

            if (!vector.HasValue ||
                Vector3.Distance(zero, targetPosition) > Vector3.Distance(vector.Value, targetPosition))
            {
                vector = zero;
            }
        }

        return vector ?? targetPosition;
    }
}
