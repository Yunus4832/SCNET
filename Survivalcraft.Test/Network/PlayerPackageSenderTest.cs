using Game.Network;
using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class PlayerPackageSenderTest
{
    [Theory]
    [InlineData(ComponentCharacterPackage.CharacterAction.AddExperience, false)]
    [InlineData(ComponentCharacterPackage.CharacterAction.SyncStat, false)]
    [InlineData(ComponentCharacterPackage.CharacterAction.PositionSet, false)]
    [InlineData(ComponentCharacterPackage.CharacterAction.IntoPlaying, true)]
    [InlineData(ComponentCharacterPackage.CharacterAction.Restart, true)]
    [InlineData(ComponentCharacterPackage.CharacterAction.Drop, true)]
    [InlineData(ComponentCharacterPackage.CharacterAction.DragDrop, true)]
    public void RequestsAndAuthoritativeResultsHaveDistinctDirections(
        ComponentCharacterPackage.CharacterAction action,
        bool server
    )
    {
        Assert.Equal(server, ComponentCharacterPackageHandler.AcceptsDirection(action, true));
        Assert.Equal(!server, ComponentCharacterPackageHandler.AcceptsDirection(action, false));
    }

    [Fact]
    public void ClientDoesNotRequireServerConnectionToMatchPayloadPlayer()
    {
        var server = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.True(ComponentCharacterPackageHandler.AcceptsSender(false, server, null));
    }

    [Fact]
    public void ServerRejectsMissingActorOrSender()
    {
        var sender = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.False(ComponentCharacterPackageHandler.AcceptsSender(true, sender, null));
        Assert.False(ComponentCharacterPackageHandler.AcceptsSender(true, null, null));
    }
}
