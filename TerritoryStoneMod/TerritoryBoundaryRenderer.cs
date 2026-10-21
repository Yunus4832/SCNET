using Engine.Core.Mathematics;
using Engine.Graphics;

using Game.Cameras;

namespace TerritoryStoneMod;

public sealed class TerritoryBoundaryRenderer : IDisposable
{
    private static readonly int[] _faceIndices = [0, 1, 2, 0, 2, 3];

    private static readonly RasterizerState _boundaryRasterizer = new()
    {
        CullMode = CullMode.None,
        // Keep coplanar terrain from winning depth tests as the camera moves.
        DepthBias = -2,
        SlopeScaleDepthBias = -1
    };

    private readonly Shader _shader = new(ReadShader("vsh"), ReadShader("psh"));

    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[16];

    private readonly int[] _indices = new int[24];

    public void Draw(Territory territory, Camera camera)
    {
        var bounds = territory.Bounds;
        var anchor = new Vector3(territory.StonePoint.X, 0, territory.StonePoint.Z);
        var min = bounds.Min - anchor;
        var max = bounds.Max - anchor;
        var width = max.X - min.X;
        SetFace(
            0,
            new Vector3(min.X, 0, min.Z),
            new Vector3(max.X, 0, min.Z),
            new Vector3(max.X, 256, min.Z),
            new Vector3(min.X, 256, min.Z),
            width
        );
        SetFace(
            4,
            new Vector3(min.X, 0, max.Z),
            new Vector3(max.X, 0, max.Z),
            new Vector3(max.X, 256, max.Z),
            new Vector3(min.X, 256, max.Z),
            width
        );
        SetFace(
            8,
            new Vector3(min.X, 0, min.Z),
            new Vector3(min.X, 0, max.Z),
            new Vector3(min.X, 256, max.Z),
            new Vector3(min.X, 256, min.Z),
            width
        );
        SetFace(
            12,
            new Vector3(max.X, 0, min.Z),
            new Vector3(max.X, 0, max.Z),
            new Vector3(max.X, 256, max.Z),
            new Vector3(max.X, 256, min.Z),
            width
        );
        var relativeCamera = camera.ViewPosition - anchor;
        var count = 0;
        AddFace(0, Math.Abs(relativeCamera.Z - min.Z));
        AddFace(4, Math.Abs(relativeCamera.Z - max.Z));
        AddFace(8, Math.Abs(relativeCamera.X - min.X));
        AddFace(12, Math.Abs(relativeCamera.X - max.X));
        if (count == 0)
        {
            return;
        }

        void AddFace(int vertex, float distance)
        {
            if (distance > 24)
            {
                return;
            }

            foreach (var offset in _faceIndices)
            {
                _indices[count++] = vertex + offset;
            }
        }

        _shader.GetParameter("u_worldViewProjectionMatrix")
            .SetValue(Matrix.CreateTranslation(anchor) * camera.ViewProjectionMatrix);
        _shader.GetParameter("u_cameraPosition").SetValue(camera.ViewPosition - anchor);
        _shader.GetParameter("u_color").SetValue(
            territory.Owner == camera.GameWidget.PlayerData.PlayerGUID
                ? new Vector4(0.2f, 0.85f, 1, 0.45f)
                : new Vector4(1, 0.7f, 0.2f, 0.45f)
        );
        var blend = Display.BlendState;
        var depth = Display.DepthStencilState;
        var rasterizer = Display.RasterizerState;
        try
        {
            Display.BlendState = BlendState.NonPremultiplied;
            Display.DepthStencilState = DepthStencilState.DepthRead;
            Display.RasterizerState = _boundaryRasterizer;
            Display.DrawUserIndexed(
                PrimitiveType.TriangleList,
                _shader,
                VertexPositionColorTexture.VertexDeclaration,
                _vertices,
                0,
                _vertices.Length,
                _indices,
                0,
                count
            );
        }
        finally
        {
            Display.BlendState = blend;
            Display.DepthStencilState = depth;
            Display.RasterizerState = rasterizer;
        }
    }

    private void SetFace(int index, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width)
    {
        _vertices[index] = new VertexPositionColorTexture(a, Color.White, Vector2.Zero);
        _vertices[index + 1] = new VertexPositionColorTexture(b, Color.White, new Vector2(width, 0));
        _vertices[index + 2] = new VertexPositionColorTexture(c, Color.White, new Vector2(width, 256));
        _vertices[index + 3] = new VertexPositionColorTexture(d, Color.White, new Vector2(0, 256));
    }

    private static string ReadShader(string extension)
    {
        using var stream = typeof(TerritoryBoundaryRenderer).Assembly.GetManifestResourceStream(
                               $"TerritoryStoneMod.Resources.Shaders.TerritoryBoundary.{extension}"
                           ) ??
                           throw new InvalidOperationException("Territory boundary shader is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose() => _shader.Dispose();
}
