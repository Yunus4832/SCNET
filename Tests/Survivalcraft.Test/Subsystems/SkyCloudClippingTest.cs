using Engine.Core.Mathematics;
using Engine.Graphics;

using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

public sealed class SkyCloudClippingTest
{
    private static readonly Plane _nearPlane = new(Vector3.UnitZ, 0.1f);

    [Theory]
    [InlineData(-1f, 3)]
    [InlineData(1f, 0)]
    public void UniformTriangleIsKeptOrRemoved(float depth, int expectedVertices)
    {
        var batch = new TexturedBatch3D();
        batch.QueueTriangle(
            new Vector3(-1, 1, depth),
            new Vector3(1, 1, depth),
            new Vector3(0, 2, depth),
            Vector2.Zero,
            Vector2.UnitX,
            Vector2.UnitY,
            Color.White
        );

        SubsystemSkyPresentation.ClipCloudsAtNearPlane(_nearPlane, batch, 0, 0);

        Assert.Equal(expectedVertices, batch.TriangleVertices.Count);
        Assert.Equal(expectedVertices, batch.TriangleIndices.Count);
        if (expectedVertices > 0)
        {
            Assert.Equal(new Vector3(-1, 1, depth), batch.TriangleVertices.Array[0].Position);
            Assert.Equal(Vector2.Zero, batch.TriangleVertices.Array[0].TexCoord);
            Assert.Equal(Color.White, batch.TriangleVertices.Array[0].Color);
        }
    }

    [Fact]
    public void EyePlaneVertexIsClippedWithContinuousTextureCoordinates()
    {
        var batch = new TexturedBatch3D();
        batch.QueueTriangle(
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(0, 2, 0),
            Vector2.Zero,
            Vector2.UnitX,
            new Vector2(0.5f, 1),
            Color.White
        );

        SubsystemSkyPresentation.ClipCloudsAtNearPlane(_nearPlane, batch, 0, 0);

        Assert.Equal(6, batch.TriangleIndices.Count);
        var intersections = new List<Vector2>();
        foreach (var vertex in batch.TriangleVertices.Array.Take(batch.TriangleVertices.Count))
        {
            Assert.True(Vector3.Dot(_nearPlane.Normal, vertex.Position) + _nearPlane.D <= 0.00001f);
            Assert.True(float.IsFinite(vertex.TexCoord.X) && float.IsFinite(vertex.TexCoord.Y));
            Assert.Equal(Color.White, vertex.Color);
            if (MathF.Abs(vertex.Position.Z + 0.1f) < 0.00001f) { intersections.Add(vertex.TexCoord); }
        }

        Assert.Contains(intersections, uv => Vector2.Distance(uv, new Vector2(0.45f, 0.9f)) < 0.00001f);
        Assert.Contains(intersections, uv => Vector2.Distance(uv, new Vector2(0.55f, 0.9f)) < 0.00001f);
    }

    [Fact]
    public void ClippingPreservesExistingBatchPrefixAndOffsetsIndices()
    {
        var batch = new TexturedBatch3D();
        batch.QueueTriangle(
            new Vector3(-1, 1, -2),
            new Vector3(1, 1, -2),
            new Vector3(0, 2, -2),
            Vector2.Zero,
            Vector2.UnitX,
            Vector2.UnitY,
            Color.Red
        );
        var prefix = batch.TriangleVertices.Array.Take(batch.TriangleVertices.Count).ToArray();
        var vertexStart = batch.TriangleVertices.Count;
        var indexStart = batch.TriangleIndices.Count;
        batch.QueueTriangle(
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(0, 2, 0),
            Vector2.Zero,
            Vector2.UnitX,
            Vector2.UnitY,
            Color.White
        );

        SubsystemSkyPresentation.ClipCloudsAtNearPlane(_nearPlane, batch, vertexStart, indexStart);

        Assert.Equal(prefix, batch.TriangleVertices.Array.Take(vertexStart));
        Assert.Equal(new[] { 0, 1, 2 }, batch.TriangleIndices.Array.Take(indexStart));
        Assert.Equal(indexStart + 6, batch.TriangleIndices.Count);
        Assert.All(
            batch.TriangleIndices.Array.Skip(indexStart).Take(6),
            index => Assert.InRange(index, vertexStart, batch.TriangleVertices.Count - 1)
        );
    }
}
