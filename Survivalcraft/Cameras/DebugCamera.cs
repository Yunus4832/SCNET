using Engine.Graphics;
using Engine.Input;

namespace Game.Cameras;

public class DebugCamera(GameWidget gameWidget) : BasePerspectiveCamera(gameWidget)
{
    public static string AmbientParameters = string.Empty;

    public static string PlantParameters = string.Empty;

    public Vector3 Direction;

    public Vector3 Position;

    public PrimitivesRenderer2D PrimitivesRenderer2D = new();

    public override bool UsesMovementControls => true;

    public override bool IsEntityControlEnabled => true;

    public override void Activate(Camera previousCamera)
    {
        Position = previousCamera.ViewPosition;
        Direction = previousCamera.ViewDirection;
        SetupPerspectiveCamera(Position, Direction, Vector3.UnitY);
    }

    public override void Update(float dt)
    {
        dt = MathUtils.Min(dt, 0.1f);
        var zero = Vector3.Zero;
        Vector2 vector;
        bool num;
        bool flag;
        if (GameWidget.PlayerData.ComponentPlayer != null)
        {
            var i = GameWidget.PlayerData.ComponentPlayer.ComponentInput.PlayerInput;
            zero = i.CameraMove;
            vector = new Vector2(i.CameraLook.X, i.CameraLook.Y);
            num = i.ToggleSneak;
            flag = i.Jump;
        }
        else
        {
            if (Keyboard.IsKeyDown(Key.A))
            {
                zero.X = -1f;
            }

            if (Keyboard.IsKeyDown(Key.D))
            {
                zero.X = 1f;
            }

            if (Keyboard.IsKeyDown(Key.W))
            {
                zero.Z = 1f;
            }

            if (Keyboard.IsKeyDown(Key.S))
            {
                zero.Z = -1f;
            }

            vector = 0.03f * new Vector2(Mouse.MouseMovement.X, -Mouse.MouseMovement.Y);
            num = Keyboard.IsKeyDown(Key.Shift);
            flag = Keyboard.IsKeyDown(Key.Control);
        }

        var direction = Direction;
        var unitY = Vector3.UnitY;
        var vector2 = Vector3.Normalize(Vector3.Cross(direction, unitY));
        var num2 = 8f;
        if (num)
        {
            num2 *= 10f;
        }

        if (flag)
        {
            num2 /= 10f;
        }

        var zero2 = Vector3.Zero;
        zero2 += num2 * zero.X * vector2;
        zero2 += num2 * zero.Y * unitY;
        zero2 += num2 * zero.Z * direction;
        Position += zero2 * dt;
        Direction = Vector3.Transform(Direction, Matrix.CreateFromAxisAngle(unitY, -4f * vector.X * dt));
        Direction = Vector3.Transform(Direction, Matrix.CreateFromAxisAngle(vector2, 4f * vector.Y * dt));
        SetupPerspectiveCamera(Position, Direction, Vector3.UnitY);
        var v = ViewportSize / 2f;
        var flatBatch2D = PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None);
        var count = flatBatch2D.LineVertices.Count;
        flatBatch2D.QueueLine(v - new Vector2(5f, 0f), v + new Vector2(5f, 0f), 0f, Color.White);
        flatBatch2D.QueueLine(v - new Vector2(0f, 5f), v + new Vector2(0f, 5f), 0f, Color.White);
        flatBatch2D.TransformLines(ViewportMatrix, count);
        PrimitivesRenderer2D.Flush();
    }
}
