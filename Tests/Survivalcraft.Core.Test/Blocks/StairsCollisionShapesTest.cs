using System.Security.Cryptography;

using Engine.Core.Mathematics;

using Game.Blocks;

namespace Survivalcraft.Core.Test.Blocks;

public sealed class StairsCollisionShapesTest
{
    [Fact]
    public void ShapesPreserveTheOriginalOrderedCoordinates()
    {
        var shapes = StairsCollisionShapes.Create();
        Assert.Equal(24, shapes.Length);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            foreach (var shape in shapes)
            {
                writer.Write(shape.Length);
                foreach (var box in shape)
                {
                    writer.Write(box.Min.X);
                    writer.Write(box.Min.Y);
                    writer.Write(box.Min.Z);
                    writer.Write(box.Max.X);
                    writer.Write(box.Max.Y);
                    writer.Write(box.Max.Z);
                }
            }
        }

        // SHA-256 of the original Initialize definitions: box count, then six float32 coordinates, little endian.
        Assert.Equal(
            "BF0B7B31CD74EDC0A18FD89A73A177BF9AE9E222430368002E19490A3AA8F8E6",
            Convert.ToHexString(SHA256.HashData(stream.ToArray()))
        );
    }

    [Fact]
    public void EveryVariantHasTheExpectedSolidVolumeAndRotationAndFlipSymmetry()
    {
        var shapes = StairsCollisionShapes.Create();
        for (var variant = 0; variant < shapes.Length; variant++)
        {
            var occupied = 0;
            var nextRotation = (variant & ~3) | ((variant + 1) & 3);
            for (var x = 0.125f; x < 1f; x += 0.25f)
            {
                for (var y = 0.125f; y < 1f; y += 0.25f)
                {
                    for (var z = 0.125f; z < 1f; z += 0.25f)
                    {
                        var solid = Contains(shapes[variant], x, y, z);
                        occupied += solid ? 1 : 0;
                        Assert.Equal(solid, Contains(shapes[variant ^ 4], x, 1f - y, z));
                        Assert.Equal(solid, Contains(shapes[nextRotation], z, y, 1f - x));
                    }
                }
            }

            var expectedVolume = (variant >> 3) switch { 0 => 0.75f, 1 => 0.625f, _ => 0.875f };
            Assert.Equal(expectedVolume, occupied / 64f);
        }
    }

    [Fact]
    public void StepBoundariesAreSolidAndTheEmptyQuarterRemainsEmpty()
    {
        var shapes = StairsCollisionShapes.Create();
        Assert.True(Contains(shapes[0], 0.5f, 0.5f, 0.75f));
        Assert.False(Contains(shapes[0], 0.5f, 0.5001f, 0.75f));
        Assert.True(Contains(shapes[0], 0.5f, 0.75f, 0.5f));
        Assert.False(Contains(shapes[0], 0.5f, 0.75f, 0.5001f));
        Assert.True(Contains(shapes[0], 0f, 0f, 0f));
        Assert.True(Contains(shapes[0], 1f, 0.5f, 1f));
        Assert.False(Contains(shapes[0], -0.0001f, 0.25f, 0.25f));
        Assert.False(Contains(shapes[0], 1.0001f, 0.25f, 0.25f));
        var independent = StairsCollisionShapes.Create();
        Assert.NotSame(shapes[0], independent[0]);
    }

    private static bool Contains(BoundingBox[] shape, float x, float y, float z)
    {
        return shape.Any(box => x >= box.Min.X &&
                                x <= box.Max.X &&
                                y >= box.Min.Y &&
                                y <= box.Max.Y &&
                                z >= box.Min.Z &&
                                z <= box.Max.Z
        );
    }
}
