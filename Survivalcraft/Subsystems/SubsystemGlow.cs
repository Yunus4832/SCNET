using Engine.Graphics;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemGlow : Subsystem, IDrawable
{
    private static readonly int[] _drawOrders = [110];

    private readonly TexturedBatch3D[] _batchesByType = new TexturedBatch3D[4];

    private readonly Dictionary<GlowPoint, bool> _glowPoints = new();

    private readonly PrimitivesRenderer3D _primitivesRenderer = new();

    private SubsystemSky _subsystemSky = null!;

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        foreach (var key in _glowPoints.Keys)
        {
            if (key.Color.A <= 0)
            {
                continue;
            }

            var vector = key.Position - camera.ViewPosition;
            var num = Vector3.Dot(vector, camera.ViewDirection);
            if (!(num > 0.01f))
            {
                continue;
            }

            var num2 = vector.Length();
            if (!(num2 < _subsystemSky.VisibilityRange))
            {
                continue;
            }

            var num3 = key.Size;
            if (key.FarDistance > 0f)
            {
                num3 += (key.FarSize - key.Size) * MathUtils.Saturate(num2 / key.FarDistance);
            }

            var v = (0f - (0.01f + 0.02f * num)) / num2 * vector;
            var color = Color.LerpNotSaturated(key.Color, _subsystemSky.ViewFogColor,
                _subsystemSky.CalculateFog(camera.ViewPosition, key.Position));
            var p = key.Position + num3 * (-key.Right - key.Up) + v;
            var p2 = key.Position + num3 * (key.Right - key.Up) + v;
            var p3 = key.Position + num3 * (key.Right + key.Up) + v;
            var p4 = key.Position + num3 * (-key.Right + key.Up) + v;

            _batchesByType[(int)key.Type].QueueQuad(p, p2, p3, p4, new Vector2(0f, 0f),
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), color);
        }

        _primitivesRenderer.Flush(camera.ViewProjectionMatrix);
    }

    public GlowPoint AddGlowPoint()
    {
        var glowPoint = new GlowPoint();
        _glowPoints.Add(glowPoint, true);
        return glowPoint;
    }

    public void RemoveGlowPoint(GlowPoint glowPoint)
    {
        _glowPoints.Remove(glowPoint);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _batchesByType[0] = _primitivesRenderer.TexturedBatch(ContentManager.Get<Texture2D>("Textures/RoundGlow"),
            false, 0, DepthStencilState.DepthRead, RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend,
            SamplerState.LinearClamp);
        _batchesByType[1] = _primitivesRenderer.TexturedBatch(ContentManager.Get<Texture2D>("Textures/SquareGlow"),
            false, 0, DepthStencilState.DepthRead, RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend,
            SamplerState.LinearClamp);
        _batchesByType[2] = _primitivesRenderer.TexturedBatch(
            ContentManager.Get<Texture2D>("Textures/HorizontalRectGlow"), false, 0, DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batchesByType[3] = _primitivesRenderer.TexturedBatch(
            ContentManager.Get<Texture2D>("Textures/VerticalRectGlow"), false, 0, DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);
    }
}
