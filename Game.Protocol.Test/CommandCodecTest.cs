using System.Text.Json.Nodes;

using Game.Protocol.Commands;
using Game.Protocol.Localization;
using Game.Protocol.Messaging;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class CommandCodecTest
{
    public static IEnumerable<object[]> Modes()
    {
        var result = new CommandResponse(true, "command.ok", "done", Data: JsonNode.Parse("{\"value\":42}"), MessageArguments: ["one"]);
        yield return [CommandPackage.CreateRequest(ResourceId.Parse("game:time"), [1, 2, 3], "typed")];
        yield return [CommandPackage.CreateResult(result, "result")];
        yield return [CommandPackage.CreatePermissionSnapshot(Guid.Parse("11111111-2222-3333-4444-555555555555"), [new CommandPermissionGrant(ResourceId.Parse("game:time"), true)])];
        yield return [CommandPackage.CreateSuggestionRequest("time ", "suggest")];
        yield return [CommandPackage.CreateSuggestions("time ", "suggest", [new CommandSuggestion("get", "fallback", false, new LocalizedText("test", "key", "fallback"))])];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryCommandModeRoundTripsWithoutRuntimeExecution(CommandPackage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter(); ProtocolMessageCodec.Write(message, writer);
        var bytes = writer.Data(CompressionPolicy.None);
        using var reader = new ProtocolReader(bytes);
        var restored = Assert.IsType<CommandPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(message.Mode, restored.Mode);
        Assert.Equal(message.CorrelationId, restored.CorrelationId);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter(); ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(bytes, repeated.Data(CompressionPolicy.None));
    }

    [Theory]
    [InlineData(CommandPackage.CommandPackageMode.Request, 4096)]
    public void ParameterPayloadLimitsAreEnforcedBeforeReadingOrWriting(
        CommandPackage.CommandPackageMode mode, int maximumLength)
    {
        var identity = ResourceId.Parse("game:time");
        var message = new CommandPackage(mode, "request")
        {
            CommandId = identity,
            Payload = new byte[maximumLength]
        };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = Assert.IsType<CommandPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(identity, restored.CommandId);
        Assert.Equal(message.Payload, restored.Payload);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);

        using var oversized = new ProtocolWriter();
        Assert.Throws<ArgumentException>(() => ProtocolMessageCodec.Write(
            message with { Payload = new byte[maximumLength + 1] }, oversized));
        using var malformed = new ProtocolWriter();
        malformed.WriteEnum(mode);
        malformed.Write("request");
        malformed.Write(identity.Namespace.Value);
        malformed.Write(identity.Path);
        malformed.Write(maximumLength + 1);
        using var malformedReader = new ProtocolReader(malformed.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read(message.ID, malformedReader));
    }

    [Fact]
    public void ResponseContractDoesNotExposeGamePublishingPolicies()
    {
        var assembly = typeof(GameProtocol).Assembly;
        Assert.Null(assembly.GetType("Game.Protocol.Commands.CommandResult"));
        Assert.DoesNotContain(typeof(CommandResponse).GetProperties(), property =>
            property.Name is "Sensitive" or "Audience");
    }

    [Fact]
    public void ResponsesCarryRequiredDataAndDisplayHints()
    {
        var response = new CommandResponse(true, "pending", "fallback", IsPending: true,
            MessageKey: "key", MessageArguments: ["value"], Data: new JsonObject { ["value"] = 42 },
            Presentation: GameMessagePresentation.History | GameMessagePresentation.Toast, CloseInputOnSuccess: true);
        var message = new CommandPackage(CommandPackage.CommandPackageMode.Result, "request") { Result = response };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = Assert.IsType<CommandPackage>(ProtocolMessageCodec.Read(message.ID, reader)).Result!;
        Assert.Equal(response.Success, restored.Success);
        Assert.Equal(response.Code, restored.Code);
        Assert.Equal(response.Message, restored.Message);
        Assert.True(restored.IsPending);
        Assert.Equal(response.MessageKey, restored.MessageKey);
        Assert.Equal(response.MessageArguments, restored.MessageArguments);
        Assert.Equal(42, restored.Data!["value"]!.GetValue<int>());
        Assert.Equal(response.Presentation,
            restored.Presentation);
        Assert.True(restored.CloseInputOnSuccess);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
