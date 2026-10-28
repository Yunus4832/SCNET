using Engine.Network.Security;

using Game.Characters;
using Game.Network;
using Game.Protocol.Identity;

namespace Survivalcraft.Test.Network;

public sealed class PlayerCreationAuthorityTest
{
    [Fact]
    public void CreationCarriesOnlyProfileAndNotClaimedIdentity()
    {
        var request = new CharacterCreateRequestPackage
        {
            CreationRequestId = Guid.NewGuid(), CharacterName = "Codex", SkinName = "$Male1",
            CharacterSex = ProtocolEnum.Encode(Game.Characters.CharacterSex.Female)
        };
        var writer = ProtocolMessageCodec.Encode(request);
        var reader = writer;
        var decoded = Assert.IsType<CharacterCreateRequestPackage>(ProtocolMessageCodec.Decode(reader));

        Assert.Equal(request.CreationRequestId, decoded.CreationRequestId);
        Assert.Equal(request.CharacterName, decoded.CharacterName);
        Assert.Equal(request.SkinName, decoded.SkinName);
        Assert.Equal(request.CharacterSex, decoded.CharacterSex);
        Assert.Null(typeof(CharacterCreateRequestPackage).GetProperty("CharacterId"));
    }

    [Fact]
    public void ExistingIdentityCannotBeReplacedByAnotherCreationRequest()
    {
        using var identity = NetworkIdentity.Create();
        var sender = new Client(null, 1, Guid.NewGuid(), Guid.Empty, null)
        {
            PlayerAddress = new PlayerAddress(identity.PublicIdentity.Address)
        };
        Assert.True(CharacterCreation.CanCreate(sender, []));
        sender.CharacterId = Guid.NewGuid();
        Assert.False(CharacterCreation.CanCreate(sender, [sender.CharacterId]));
        var replacement = new Client(null, 1, Guid.NewGuid(), sender.CharacterId, null)
            { PlayerAddress = sender.PlayerAddress };
        Assert.False(CharacterCreation.CanCreate(replacement, [sender.CharacterId]));
    }

    [Fact]
    public void MissingOrEmptyConnectionIdentityCannotCreatePlayer()
    {
        Assert.False(CharacterCreation.CanCreate(null, []));
        Assert.False(CharacterCreation.CanCreate(new Client(null, 1, Guid.NewGuid(), Guid.Empty, null), []));
    }
}
