using Engine.Network.Security;

using Game.Network;
using Game.Network.Handling;
using Game.Protocol.Identity;

namespace Survivalcraft.Test.Network;

public sealed class PlayerCreationAuthorityTest
{
    [Fact]
    public void CreationCarriesOnlyProfileAndNotClaimedIdentity()
    {
        var request = new CharacterDataPackage
        {
            Type = CharacterDataPackage.DataType.Create,
            CreationRequestId = Guid.NewGuid(),
            CharacterName = "Codex",
            SkinName = "$Male1",
            CharacterSex = ProtocolEnum.Encode(Game.CharacterSex.Female),
            CharacterId = Guid.NewGuid()
        };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(request, writer);
        using var reader = new ProtocolReader(writer.Data());
        var decoded = Assert.IsType<CharacterDataPackage>(ProtocolMessageCodec.Read(request.ID, reader));

        Assert.Equal(request.CreationRequestId, decoded.CreationRequestId);
        Assert.Equal(request.CharacterName, decoded.CharacterName);
        Assert.Equal(request.SkinName, decoded.SkinName);
        Assert.Equal(request.CharacterSex, decoded.CharacterSex);
        Assert.Equal(Guid.Empty, decoded.CharacterId);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void ExistingIdentityCannotBeReplacedByAnotherCreationRequest()
    {
        using var identity = NetworkIdentity.Create();
        var sender = new Client(null, 1, Guid.NewGuid(), Guid.Empty, null)
        {
            PlayerAddress = new PlayerAddress(identity.PublicIdentity.Address)
        };
        Assert.True(CharacterDataPackageHandler.CanCreateCharacter(sender, []));
        sender.CharacterId = Guid.NewGuid();
        Assert.False(CharacterDataPackageHandler.CanCreateCharacter(sender, [sender.CharacterId]));
        var replacement = new Client(null, 1, Guid.NewGuid(), sender.CharacterId, null)
            { PlayerAddress = sender.PlayerAddress };
        Assert.False(CharacterDataPackageHandler.CanCreateCharacter(replacement, [sender.CharacterId]));
    }

    [Fact]
    public void MissingOrEmptyConnectionIdentityCannotCreatePlayer()
    {
        Assert.False(CharacterDataPackageHandler.CanCreateCharacter(null, []));
        Assert.False(
            CharacterDataPackageHandler.CanCreateCharacter(new Client(null, 1, Guid.NewGuid(), Guid.Empty, null), [])
        );
    }
}
