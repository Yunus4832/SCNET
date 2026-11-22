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
        bool fastMovement;
        bool slowMovement;
        if (GameWidget.CharacterData.ComponentCharacter != null)
        {
            var i = GameWidget.CharacterData.ComponentCharacter.Entity.FindComponent<ComponentInput>(true)!.PlayerInput;
            zero = i.CameraMove;
            vector = new Vector2(i.CameraLook.X, i.CameraLook.Y);
            fastMovement = i.ToggleSneak;
            slowMovement = i.Jump;
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
            fastMovement = Keyboard.IsKeyDown(Key.Shift);
            slowMovement = Keyboard.IsKeyDown(Key.Control);
        }

        var direction = Direction;
        var unitY = Vector3.UnitY;
        var cameraRight = Vector3.Normalize(Vector3.Cross(direction, unitY));
        var moveSpeed = 8f;
        if (fastMovement)
        {
            moveSpeed *= 10f;
        }

        if (slowMovement)
        {
            moveSpeed /= 10f;
        }

        var zero2 = Vector3.Zero;
        zero2 += moveSpeed * zero.X * cameraRight;
        zero2 += moveSpeed * zero.Y * unitY;
        zero2 += moveSpeed * zero.Z * direction;
        Position += zero2 * dt;
        Direction = Vector3.Transform(Direction, Matrix.CreateFromAxisAngle(unitY, -4f * vector.X * dt));
        Direction = Vector3.Transform(Direction, Matrix.CreateFromAxisAngle(cameraRight, 4f * vector.Y * dt));
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
