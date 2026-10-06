using Game.Network;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class PlayerPackageSenderTest
{
    [Theory]
    [InlineData(ComponentPlayerPackage.PlayerAction.AddExperience, false)]
    [InlineData(ComponentPlayerPackage.PlayerAction.SyncStat, false)]
    [InlineData(ComponentPlayerPackage.PlayerAction.PositionSet, false)]
    [InlineData(ComponentPlayerPackage.PlayerAction.IntoPlaying, true)]
    [InlineData(ComponentPlayerPackage.PlayerAction.Restart, true)]
    [InlineData(ComponentPlayerPackage.PlayerAction.Drop, true)]
    [InlineData(ComponentPlayerPackage.PlayerAction.DragDrop, true)]
    public void RequestsAndAuthoritativeResultsHaveDistinctDirections(ComponentPlayerPackage.PlayerAction action,
        bool server)
    {
        Assert.Equal(server, ComponentPlayerPackageHandler.AcceptsDirection(action, true));
        Assert.Equal(!server, ComponentPlayerPackageHandler.AcceptsDirection(action, false));
    }

    [Fact]
    public void ClientDoesNotRequireServerConnectionToMatchPayloadPlayer()
    {
        var server = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.True(ComponentPlayerPackageHandler.AcceptsSender(false, server, null));
    }

    [Fact]
    public void ServerRejectsMissingActorOrSender()
    {
        var sender = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.False(ComponentPlayerPackageHandler.AcceptsSender(true, sender, null));
        Assert.False(ComponentPlayerPackageHandler.AcceptsSender(true, null, null));
    }
}
