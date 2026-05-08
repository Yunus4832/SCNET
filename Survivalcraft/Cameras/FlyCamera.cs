using Engine.Input;

namespace Game.Cameras;

public class FlyCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    private Vector3 _direction;

    private float _pitchSpeed;

    private Vector3 _position;

    private float _rollAngle;

    private float _rollSpeed;

    private Vector3 _velocity;

    public override bool UsesMovementControls => true;

    public override bool IsEntityControlEnabled => false;

    public override void Activate(Camera previousCamera)
    {
        _position = previousCamera.ViewPosition;
        _direction = previousCamera.ViewDirection;
        SetupPerspectiveCamera(_position, _direction, Vector3.UnitY);
    }

    public override void Update(float dt)
    {
        var vector = Vector3.Zero;
        var vector2 = Vector2.Zero;
        var componentInput = GameWidget.PlayerData.ComponentPlayer?.ComponentInput;
        if (componentInput != null)
        {
            vector = componentInput.PlayerInput.CameraMove * new Vector3(1f, 0f, 1f);
            vector2 = componentInput.PlayerInput.CameraLook;
        }

        var num = Keyboard.IsKeyDown(Key.Shift);
        var flag = Keyboard.IsKeyDown(Key.Control);
        var direction = _direction;
        var unitY = Vector3.UnitY;
        var vector3 = Vector3.Normalize(Vector3.Cross(direction, unitY));
        var num2 = 10f;
        if (num)
        {
            num2 *= 5f;
        }

        if (flag)
        {
            num2 /= 5f;
        }

        var zero = Vector3.Zero;
        zero += num2 * vector.X * vector3;
        zero += num2 * vector.Y * unitY;
        zero += num2 * vector.Z * direction;
        _rollSpeed = MathUtils.Lerp(_rollSpeed, -1.5f * vector2.X, 3f * dt);
        _rollAngle += _rollSpeed * dt;
        _rollAngle *= MathUtils.Pow(0.33f, dt);
        _pitchSpeed = MathUtils.Lerp(_pitchSpeed, -0.2f * vector2.Y, 3f * dt);
        _pitchSpeed *= MathUtils.Pow(0.33f, dt);
        _velocity += 1.5f * (zero - _velocity) * dt;
        _position += _velocity * dt;
        _direction = Vector3.Transform(_direction, Matrix.CreateFromAxisAngle(unitY, 0.05f * _rollAngle));
        _direction = Vector3.Transform(_direction, Matrix.CreateFromAxisAngle(vector3, 0.2f * _pitchSpeed));
        var up = Vector3.TransformNormal(Vector3.UnitY, Matrix.CreateFromAxisAngle(_direction, 0f - _rollAngle));
        SetupPerspectiveCamera(_position, _direction, up);
    }
}
