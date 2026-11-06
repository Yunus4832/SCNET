using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemGlowPresentation : Subsystem, IDrawable
{
    private static readonly int[] _drawOrders = [110];

    private readonly TexturedBatch3D[] _batchesByType = new TexturedBatch3D[4];

    private SubsystemGlow _glow = null!;

    private PrimitivesRenderer3D? _renderer;

    private bool _disposed;

    private PrimitivesRenderer3D PrimitivesRenderer3D => _renderer ??= new();

    private SubsystemSkyPresentation _subsystemSky = null!;

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        foreach (var key in _glow.Points)
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
            var color = Color.LerpNotSaturated(
                key.Color,
                _subsystemSky.ViewFogColor,
                _subsystemSky.CalculateFog(camera.ViewPosition, key.Position)
            );
            var p = key.Position + num3 * (-key.Right - key.Up) + v;
            var p2 = key.Position + num3 * (key.Right - key.Up) + v;
            var p3 = key.Position + num3 * (key.Right + key.Up) + v;
            var p4 = key.Position + num3 * (-key.Right + key.Up) + v;

            _batchesByType[(int)key.Type].QueueQuad(
                p,
                p2,
                p3,
                p4,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                color
            );
        }

        PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _glow = Project.FindSubsystem<SubsystemGlow>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;

        _batchesByType[0] = PrimitivesRenderer3D.TexturedBatch(
            ContentManager.Get<Texture2D>("Textures/RoundGlow"),
            false,
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp
        );
        _batchesByType[1] = PrimitivesRenderer3D.TexturedBatch(
            ContentManager.Get<Texture2D>("Textures/SquareGlow"),
            false,
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp
        );
        _batchesByType[2] = PrimitivesRenderer3D.TexturedBatch(
            ContentManager.Get<Texture2D>("Textures/HorizontalRectGlow"),
            false,
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp
        );
        _batchesByType[3] = PrimitivesRenderer3D.TexturedBatch(
            ContentManager.Get<Texture2D>("Textures/VerticalRectGlow"),
            false,
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp
        );
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _renderer?.Clear();
        _renderer = null;
        Array.Clear(_batchesByType);
    }
}
