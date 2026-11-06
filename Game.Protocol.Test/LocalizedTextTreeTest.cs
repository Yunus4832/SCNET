using Game.Protocol.Entities;
using Game.Protocol.Localization;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class LocalizedTextTreeTest
{
    [Theory]
    [InlineData(99, 6)]
    [InlineData(0, 5)]
    public void MalformedWireKindAndShapeAreRejected(int kind, int fields)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePack.MessagePackWriter(buffer);
        writer.WriteArrayHeader(fields);
        writer.Write(kind);
        writer.Write("");
        writer.Write("");
        writer.Write("literal");
        writer.WriteNil();
        writer.WriteArrayHeader(0);
        writer.Flush();
        Assert.ThrowsAny<Exception>(() =>
            MessagePack.MessagePackSerializer.Deserialize<LocalizedText>(buffer.WrittenMemory)
        );
    }

    [Fact]
    public void WidgetResourceCauseRoundTripsItsCanonicalAddress()
    {
        var source = LocalizedText.ContentWidget("VitalStatsWidget", "Choked", "Choked");
        var package = new EntityHealthPackage { TargetId = 1, Health = 0, Cause = source };
        var decoded =
            Assert.IsType<EntityHealthPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package)));
        Assert.Equal(source, decoded.Cause);
    }

    [Fact]
    public void EntityCauseRoundTripsNestedDatabaseAndLiteralArguments()
    {
        var source = LocalizedText.Format(
            new LocalizedText("Miner", "4", "{0} {1}"),
            LocalizedText.Database("DisplayName", "Wolf", "Wolf"),
            LocalizedText.Literal("comma, semicolon;")
        );
        var package = new EntityHealthPackage { TargetId = 1, Health = 0, Cause = source };
        var decoded =
            Assert.IsType<EntityHealthPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package)));
        Assert.Equal(source, decoded.Cause);
        Assert.NotSame(source.Arguments, decoded.Cause.Arguments);
    }

    [Fact]
    public void ExcessDepthNodesAndUtf8BytesAreRejected()
    {
        var deep = LocalizedText.Empty;
        for (var i = 0; i <= LocalizedTextLimits.MaximumDepth; i++) { deep = LocalizedText.Format(deep); }

        Assert.Throws<InvalidDataException>(() => LocalizedTextLimits.Validate(deep));
        var broad = LocalizedText.Format(
            LocalizedText.Literal("{0}"),
            Enumerable.Repeat(LocalizedText.Empty, LocalizedTextLimits.MaximumNodes).ToArray()
        );
        Assert.Throws<InvalidDataException>(() => LocalizedTextLimits.Validate(broad));
        Assert.Throws<InvalidDataException>(() => LocalizedTextLimits.Validate(
                LocalizedText.Literal(new string('界', LocalizedTextLimits.MaximumStringBytes / 3 + 1))
            )
        );
    }

    [Fact]
    public void ConstructorCopiesArgumentArray()
    {
        var arguments = new[] { LocalizedText.Literal("original") };
        var text = LocalizedText.Format(LocalizedText.Literal("{0}"), arguments);
        arguments[0] = LocalizedText.Literal("changed");
        Assert.Equal("original", text.Arguments[0].Fallback);
    }
}
