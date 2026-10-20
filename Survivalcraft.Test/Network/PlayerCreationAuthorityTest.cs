using Game.Network;
using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class PlayerCreationAuthorityTest
{
    [Fact]
    public void CreationCarriesOnlyProfileAndNotClaimedIdentity()
    {
        var request = new PlayerDataPackage
        {
            Type = PlayerDataPackage.DataType.Create,
            PlayerName = "Codex",
            SkinName = "$Male1",
            PlayerClass = ProtocolEnum.Encode(Game.PlayerClass.Female),
            PlayerGuid = Guid.NewGuid()
        };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(request, writer);
        using var reader = new ProtocolReader(writer.Data());
        var decoded = Assert.IsType<PlayerDataPackage>(ProtocolMessageCodec.Read(request.ID, reader));

        Assert.Equal(request.PlayerName, decoded.PlayerName);
        Assert.Equal(request.SkinName, decoded.SkinName);
        Assert.Equal(request.PlayerClass, decoded.PlayerClass);
        Assert.Equal(Guid.Empty, decoded.PlayerGuid);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void ExistingIdentityCannotBeReplacedByAnotherCreationRequest()
    {
        var sender = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        Assert.True(PlayerDataPackageHandler.CanCreatePlayer(sender, []));
        Assert.False(PlayerDataPackageHandler.CanCreatePlayer(sender, [sender.GUID]));
        var replacement = new Client(null, 1, Guid.NewGuid(), sender.GUID, null);
        Assert.False(PlayerDataPackageHandler.CanCreatePlayer(replacement, [sender.GUID]));
    }

    [Fact]
    public void MissingOrEmptyConnectionIdentityCannotCreatePlayer()
    {
        Assert.False(PlayerDataPackageHandler.CanCreatePlayer(null, []));
        Assert.False(PlayerDataPackageHandler.CanCreatePlayer(
            new Client(null, 1, Guid.NewGuid(), Guid.Empty, null), []));
    }
}
