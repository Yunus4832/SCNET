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
        var package = new ClientPackage([new ClientIdentity(1, token, character, address, ClientState.Playing)]);
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var restored = Assert.IsType<ClientPackage>(ProtocolMessageCodec.Read(package.ID, reader));
        var client = Assert.Single(restored.List);
        Assert.Equal(address, client.PlayerAddress);
        Assert.Equal(character, client.CharacterId);
        Assert.Equal(token, client.TokenId);
        Assert.Equal(ClientState.Playing, client.State);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void RemoteDirectoryEntryCannotContainOnlyACharacterIdentifier()
    {
        var package = new ClientPackage(1, Guid.NewGuid(), Guid.NewGuid(), null);
        using var writer = new ProtocolWriter();
        Assert.Throws<ArgumentException>(() => ProtocolMessageCodec.Write(package, writer));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(43)]
    [InlineData(44)]
    [InlineData(255)]
    public void InvalidAddressLengthsAreRejectedBeforeReadingAddressData(byte size)
    {
        using var writer = new ProtocolWriter();
        writer.WriteEnum(ClientPackage.EventType.Add);
        writer.Write((byte)1);
        writer.Write(Guid.NewGuid());
        writer.Write(Guid.NewGuid());
        writer.Write(size);
        using var reader = new ProtocolReader(writer.Data());
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.Client, reader));
    }
}
