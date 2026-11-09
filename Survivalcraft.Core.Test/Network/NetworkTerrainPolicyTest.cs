using Engine.Core.Mathematics;

using Game.Network.Chunks;
using Game.Protocol.Terrain;

namespace Survivalcraft.Core.Test.Network;

public sealed class NetworkTerrainPolicyTest
{
    [Theory]
    [InlineData(float.NaN, 128f)]
    [InlineData(float.PositiveInfinity, 128f)]
    [InlineData(float.NegativeInfinity, 128f)]
    [InlineData(128f, float.NaN)]
    [InlineData(128f, float.PositiveInfinity)]
    [InlineData(128f, float.NegativeInfinity)]
    public void NonFiniteDistancesAreRejectedBeforeReachingDemandValidation(float visibility, float content)
    {
        var requested = new TerrainUpdateLocation
        {
            Center = Vector2.Zero,
            VisibilityDistance = visibility,
            ContentDistance = content
        };
        Assert.False(NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, Vector2.Zero, out _));
    }

    [Theory]
    [InlineData(2, 0, true, false)]
    [InlineData(-3, -1, true, false)]
    [InlineData(1, 0, true, true)]
    [InlineData(-2, -1, true, true)]
    [InlineData(2, 1, false, false)]
    public void ContentResidencyDoesNotExpandToBroadphaseOnlyChunks(int x, int z, bool broadphase, bool content)
    {
        var coords = new Point2(x, z);
        var chunkCenter = new Vector2((x + 0.5f) * 16f, (z + 0.5f) * 16f);
        Assert.Equal(broadphase, NetworkTerrainPolicy.IsChunkRelevant(coords, Vector2.Zero, 32));
        Assert.Equal(content, NetworkTerrainPolicy.IsChunkCenterRelevant(coords, Vector2.Zero, 32));
        Assert.Equal(content, NetworkTerrainPolicy.IsChunkCenterRelevant(chunkCenter, Vector2.Zero, 32));
    }

    [Theory]
    [InlineData(2, 0, true)]
    [InlineData(-2, 0, true)]
    [InlineData(0, 2, true)]
    [InlineData(0, -2, true)]
    [InlineData(2, 1, false)]
    public void ContentCircleIncludesItsExactPositiveAndNegativeCenterBoundaries(int x, int z, bool expected)
    {
        Assert.Equal(expected, NetworkTerrainPolicy.IsChunkCenterRelevant(new Point2(x, z), new Vector2(8), 32));
    }

    [Fact]
    public void OriginalRequestCanBeReapprovedAfterDelayedPlayerMovement()
    {
        var requested = new TerrainUpdateLocation
        {
            Center = new Vector2(600, 0),
            VisibilityDistance = 128f,
            ContentDistance = 128f
        };

        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, Vector2.Zero, out var beforeMovement)
        );
        Assert.Equal(new Vector2(64, 0), beforeMovement.Center);
        Assert.Equal(new Vector2(600, 0), requested.Center);

        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, requested.Center, out var afterMovement)
        );
        Assert.Equal(requested.Center, afterMovement.Center);

        requested.Center = Vector2.Zero;
        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, new Vector2(600, 0), out var beforeReturn)
        );
        Assert.Equal(new Vector2(536, 0), beforeReturn.Center);
        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, Vector2.Zero, out var afterReturn)
        );
        Assert.Equal(Vector2.Zero, afterReturn.Center);
    }

    [Fact]
    public void ClientTerrainRequestIsClampedToServerLimit()
    {
        var requested = new TerrainUpdateLocation
        {
            Center = new Vector2(10f, 20f),
            VisibilityDistance = ushort.MaxValue,
            ContentDistance = ushort.MaxValue
        };

        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(
                requested,
                512,
                requested.Center,
                out var clamped
            )
        );
        Assert.Equal(512f, clamped.VisibilityDistance);
        Assert.Equal(512f, clamped.ContentDistance);
    }

    [Fact]
    public void ContentDistanceCannotBeLowerThanVisibilityDistance()
    {
        var requested = new TerrainUpdateLocation
        {
            Center = Vector2.Zero,
            VisibilityDistance = 384f,
            ContentDistance = 32f
        };

        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(
                requested,
                512,
                requested.Center,
                out var clamped
            )
        );
        Assert.Equal(384f, clamped.ContentDistance);
    }

    [Fact]
    public void NonFiniteCenterIsRejected()
    {
        var requested = new TerrainUpdateLocation
        {
            Center = new Vector2(float.NaN, 0f),
            VisibilityDistance = 128f,
            ContentDistance = 128f
        };

        Assert.False(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(
                requested,
                512,
                Vector2.Zero,
                out _
            )
        );
    }

    [Fact]
    public void ClientCenterIsLimitedAroundAuthoritativePlayerPosition()
    {
        var requested = new TerrainUpdateLocation
        {
            Center = new Vector2(1000f, 0f),
            VisibilityDistance = 128f,
            ContentDistance = 128f
        };

        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(
                requested,
                512,
                Vector2.Zero,
                out var clamped
            )
        );
        Assert.Equal(NetworkTerrainPolicy.MaximumClientCameraOffset, clamped.Center.X);
        Assert.Equal(0f, clamped.Center.Y);
    }
}
