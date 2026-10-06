using Engine.Core;

using Game.Network;
using Game.Terrains;

namespace Survivalcraft.Test.Network;

public sealed class NetworkTerrainPolicyTest
{
    [Fact]
    public void OriginalRequestCanBeReapprovedAfterDelayedPlayerMovement()
    {
        var requested = new TerrainUpdater.UpdateLocation
        {
            Center = new Vector2(600, 0),
            VisibilityDistance = 128f,
            ContentDistance = 128f
        };

        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested, 512, Vector2.Zero, out var beforeMovement));
        Assert.Equal(new Vector2(64, 0), beforeMovement.Center);
        Assert.Equal(new Vector2(600, 0), requested.Center);

        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested, 512, requested.Center, out var afterMovement));
        Assert.Equal(requested.Center, afterMovement.Center);

        requested.Center = Vector2.Zero;
        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested, 512, new Vector2(600, 0), out var beforeReturn));
        Assert.Equal(new Vector2(536, 0), beforeReturn.Center);
        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested, 512, Vector2.Zero, out var afterReturn));
        Assert.Equal(Vector2.Zero, afterReturn.Center);
    }

    [Fact]
    public void ClientTerrainRequestIsClampedToServerLimit()
    {
        var requested = new TerrainUpdater.UpdateLocation
        {
            Center = new Vector2(10f, 20f),
            VisibilityDistance = ushort.MaxValue,
            ContentDistance = ushort.MaxValue
        };

        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested,
            512,
            requested.Center,
            out var clamped));
        Assert.Equal(512f, clamped.VisibilityDistance);
        Assert.Equal(512f, clamped.ContentDistance);
    }

    [Fact]
    public void ContentDistanceCannotBeLowerThanVisibilityDistance()
    {
        var requested = new TerrainUpdater.UpdateLocation
        {
            Center = Vector2.Zero,
            VisibilityDistance = 384f,
            ContentDistance = 32f
        };

        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested,
            512,
            requested.Center,
            out var clamped));
        Assert.Equal(384f, clamped.ContentDistance);
    }

    [Fact]
    public void NonFiniteCenterIsRejected()
    {
        var requested = new TerrainUpdater.UpdateLocation
        {
            Center = new Vector2(float.NaN, 0f),
            VisibilityDistance = 128f,
            ContentDistance = 128f
        };

        Assert.False(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested,
            512,
            Vector2.Zero,
            out _));
    }

    [Fact]
    public void ClientCenterIsLimitedAroundAuthoritativePlayerPosition()
    {
        var requested = new TerrainUpdater.UpdateLocation
        {
            Center = new Vector2(1000f, 0f),
            VisibilityDistance = 128f,
            ContentDistance = 128f
        };

        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(
            requested,
            512,
            Vector2.Zero,
            out var clamped));
        Assert.Equal(NetworkTerrainPolicy.MaximumClientCameraOffset, clamped.Center.X);
        Assert.Equal(0f, clamped.Center.Y);
    }
}
