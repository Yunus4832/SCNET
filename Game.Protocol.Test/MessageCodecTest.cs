using System.Text.Json;

using Game.Protocol.Connection;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class MessageCodecTest
{
    [Theory]
    [InlineData("connection-request")]
    [InlineData("connection-reject")]
    public void RecordCodecMatchesIndependentWireSample(string name)
    {
        var frame = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".bin"));
        using var reader = new ProtocolReader(frame);
        Assert.Equal(0x88, reader.ReadByte());
        var id = reader.ReadByte();
        var message = ProtocolMessageCodec.Read(id, reader);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var writer = new ProtocolWriter();
        writer.Write((byte)0x88);
        writer.Write(message.ID);
        ProtocolMessageCodec.Write(message, writer);
        Assert.Equal(frame, writer.Data(CompressionPolicy.None));
        if (message is ConnectionRequestPackage request)
        {
            Assert.Equal(1, request.ProtocolVersion);
            Assert.Equal(GameProtocol.ConnectionMagic, request.Magic);
            Assert.Equal(new string('a', 64), request.ModDataHash);
        }
        else
        {
            Assert.Equal("拒绝 test", Assert.IsType<ConnectionRejectPackage>(message).Reason);
        }
    }

    [Fact]
    public void MessageIdsMatchTheExplicitUniqueAssignments()
    {
        var assignments = JsonSerializer.Deserialize<Dictionary<string, byte>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "package-ids.json")))!;
        Assert.Equal(assignments.Count, Enum.GetValues<PackageType>().Length);
        foreach (var (name, value) in assignments)
        {
            Assert.Equal(value, (byte)Enum.Parse<PackageType>(name));
            Assert.True(ProtocolMessageCodec.IsDefined(value));
        }

        var assignedIds = assignments.Values.ToHashSet();
        Assert.Equal(assignments.Count, assignedIds.Count);
        for (var id = 0; id <= byte.MaxValue; id++)
        {
            Assert.Equal(assignedIds.Contains((byte)id), ProtocolMessageCodec.IsDefined((byte)id));
        }
    }

    [Fact]
    public void MalformedMessagesFailInsteadOfReturningIncompleteRecords()
    {
        using var unknown = new ProtocolReader(new MemoryStream());
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read(250, unknown));
        using var truncated = new ProtocolReader(new MemoryStream(new byte[8]));
        Assert.Throws<EndOfStreamException>(() => ProtocolMessageCodec.Read((byte)PackageType.ConnectionRequest, truncated));
    }
}
