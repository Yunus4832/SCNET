using Engine.Core;

using Game;
using Game.Network;
using Game.Terrains;

namespace Survivalcraft.Test.Network;

public sealed class NetworkTerrainPolicyTest
{
    [Fact]
    public void ClientTerrainRequestIsClampedToServerLimit()
    {
        var requested = new TerrainUpdater.UpdateLocation
        {
            Center = new Vector2(10f, 20f),
            VisibilityDistance = ushort.MaxValue,
            ContentDistance = ushort.MaxValue
        };

        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, out var clamped));
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

        Assert.True(NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, out var clamped));
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

        Assert.False(NetworkTerrainPolicy.TryClampClientUpdateLocation(requested, 512, out _));
    }
}
