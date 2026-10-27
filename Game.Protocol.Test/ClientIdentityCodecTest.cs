using Engine.Core.Security;

using Game.Protocol.Connection;
using Game.Protocol.Identity;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class ClientIdentityCodecTest
{
    [Fact]
    public void UppercaseInputHasTheSameCanonicalSubjectIdentity()
    {
        var address = IdentityAddress.Encode(new byte[32]);
        var upper = new PlayerAddress(address.ToUpperInvariant());
        Assert.Equal(new PlayerAddress(address), upper);
        Assert.Equal(address, upper.Value);
    }

    [Fact]
    public void DirectorySeparatesSubjectCharacterAndSessionState()
    {
        var token = Guid.NewGuid();
        var character = Guid.NewGuid();
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var package = new ClientListPackage
            { List = [new ClientIdentity(1, token, character, address, ClientState.Playing)] };
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var restored = Assert.IsType<ClientListPackage>(ProtocolMessageCodec.Decode(reader));
        var client = Assert.Single(restored.List);
        Assert.Equal(address, client.PlayerAddress);
        Assert.Equal(character, client.CharacterId);
        Assert.Equal(token, client.TokenId);
        Assert.Equal(ClientState.Playing, client.State);
    }

    [Fact]
    public void RemoteDirectoryEntryCannotContainOnlyACharacterIdentifier()
    {
        var package = new ClientAddedPackage { Client = new ClientIdentity(1, Guid.NewGuid(), Guid.NewGuid(), null) };
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(package));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(43)]
    [InlineData(44)]
    [InlineData(255)]
    public void InvalidAddressLengthsAreRejectedBeforeReadingAddressData(byte size)
    {
        var payload = MessagePack.MessagePackSerializer.Serialize(
            new object[] { 1, Guid.NewGuid(), Guid.NewGuid(), new string('a', size), (byte)ClientState.NotConnected }
        );
        var package = new ClientAddedPackage
        {
            Client = new ClientIdentity(
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                new PlayerAddress(IdentityAddress.Encode(new byte[32]))
            )
        };
        var malformed = WireTestData.ReplaceField(package, 0, payload);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
