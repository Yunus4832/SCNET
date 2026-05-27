using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentAimingSights : Component, IUpdateable, IDrawable
{
    private static readonly int[] _drawOrders = [2000];

    private readonly PrimitivesRenderer2D _primitivesRenderer2D = new();

    private readonly PrimitivesRenderer3D _primitivesRenderer3D = new();

    private ComponentPlayer _componentPlayer = null!;

    private Vector3 _sightsDirection;

    private Vector3 _sightsPosition;

    public bool IsSightsVisible { get; set; }

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (camera.GameWidget != _componentPlayer.GameWidget)
        {
            return;
        }

        if (_componentPlayer.ComponentHealth.Health > 0f &&
            _componentPlayer.ComponentGui.ControlsContainerWidget.IsVisible)
        {
            if (IsSightsVisible)
            {
                var texture = ContentManager.Get<Texture2D>("Textures/Gui/Sights");
                var s = !camera.Eye.HasValue ? 8f : 2.5f;
                var v = _sightsPosition + _sightsDirection * 50f;
                var vector = Vector3.Normalize(Vector3.Cross(_sightsDirection, Vector3.UnitY));
                var v2 = Vector3.Normalize(Vector3.Cross(_sightsDirection, vector));
                var p = v + s * (-vector - v2);
                var p2 = v + s * (vector - v2);
                var p3 = v + s * (vector + v2);
                var p4 = v + s * (-vector + v2);
                var texturedBatch3D = _primitivesRenderer3D.TexturedBatch(texture, false, 0, DepthStencilState.None);
                var count = texturedBatch3D.TriangleVertices.Count;
                texturedBatch3D.QueueQuad(p, p2, p3, p4, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(0f, 1f), Color.White);
                texturedBatch3D.TransformTriangles(camera.ViewMatrix, count);
            }

            if (!camera.Eye.HasValue && !camera.UsesMovementControls && !IsSightsVisible &&
                (SettingsManager.LookControlMode == LookControlMode.SplitTouch ||
                 !_componentPlayer.ComponentInput.IsControlledByTouch))
            {
                var subtexture = ContentManager.Get<Subtexture>("Textures/Atlas/Crosshair");
                var s2 = 1.25f;
                var v3 = camera.ViewPosition + camera.ViewDirection * 50f;
                var vector2 = Vector3.Normalize(Vector3.Cross(camera.ViewDirection, Vector3.UnitY));
                var v4 = Vector3.Normalize(Vector3.Cross(camera.ViewDirection, vector2));
                var p5 = v3 + s2 * (-vector2 - v4);
                var p6 = v3 + s2 * (vector2 - v4);
                var p7 = v3 + s2 * (vector2 + v4);
                var p8 = v3 + s2 * (-vector2 + v4);
                var texturedBatch3D2 =
                    _primitivesRenderer3D.TexturedBatch(subtexture.Texture, false, 0, DepthStencilState.None);
                var count2 = texturedBatch3D2.TriangleVertices.Count;
                texturedBatch3D2.QueueQuad(p5, p6, p7, p8, new Vector2(subtexture.TopLeft.X, subtexture.TopLeft.Y),
                    new Vector2(subtexture.BottomRight.X, subtexture.TopLeft.Y),
                    new Vector2(subtexture.BottomRight.X, subtexture.BottomRight.Y),
                    new Vector2(subtexture.TopLeft.X, subtexture.BottomRight.Y), Color.White);
                texturedBatch3D2.TransformTriangles(camera.ViewMatrix, count2);
            }
        }

        _primitivesRenderer2D.Flush();
        _primitivesRenderer3D.Flush(camera.ProjectionMatrix);
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Reset;

    public void Update(float dt)
    {
        IsSightsVisible = false;
    }

    public void ShowAimingSights(Vector3 position, Vector3 direction)
    {
        IsSightsVisible = true;
        _sightsPosition = position;
        _sightsDirection = direction;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
    }
}
