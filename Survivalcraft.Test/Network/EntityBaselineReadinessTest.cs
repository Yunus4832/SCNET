using Game.Network.Enums;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class EntityBaselineReadinessTest
{
    [Theory]
    [InlineData(ClientState.NotConnected, false)]
    [InlineData(ClientState.Connected, false)]
    [InlineData(ClientState.ProjectLoaded, true)]
    [InlineData(ClientState.LoadTerrain, true)]
    [InlineData(ClientState.Playing, true)]
    public void OwnEntityBaselineDoesNotRequirePlaying(ClientState state, bool expected)
    {
        Assert.Equal(expected, SubsystemBodies.CanReceiveEntityBaseline(state));
    }
}
