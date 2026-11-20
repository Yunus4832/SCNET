using Engine.Core.Mathematics;

using Game.Blocks.Collision;

namespace Survivalcraft.Core.Test.Blocks.Collision;

public sealed class CollisionGeometryBuilderTest
{
    [Fact]
    public void ModelsCopyTheirInputAndBuildersNeverShareMutableWorldWork()
    {
        var positions = new[] { Vector3.Zero, Vector3.One };
        var primitive = new CollisionPrimitiveGeometry(positions);
        positions[1] = new Vector3(100f);
        var first = new CollisionGeometryBuilder();
        var second = new CollisionGeometryBuilder();
        first.Append(primitive, Matrix.Identity);
        second.Append(primitive, Matrix.Identity);
        first.Append(primitive, Matrix.CreateTranslation(new Vector3(10f)));
        Assert.Equal(2, second.Count);
        Assert.Equal(new BoundingBox(Vector3.Zero, Vector3.One), second.GetBounds());
        Assert.Equal(new Vector3(11f), first.GetBounds().Max);
        Assert.Equal(Vector3.One, primitive.Positions[1]);
    }

    [Fact]
    public void EmptyBuilderRetainsTheExistingEmptyBoundsSemantics()
    {
        var builder = new CollisionGeometryBuilder();
        Assert.Equal(0, builder.Count);
        Assert.Throws<ArgumentException>(() => builder.GetBounds());
        Assert.Throws<ArgumentException>(() => builder.GetBounds(Matrix.Identity));
    }
}
