using System.Text.Json.Nodes;

using Engine.Core.Security;

using Game.Protocol.Commands;
using Game.Protocol.Identity;
using Game.Protocol.Localization;
using Game.Protocol.Messaging;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class CommandCodecTest
{
    public static IEnumerable<object[]> Modes()
    {
        var result = new CommandResponse(
            true,
            "command.ok",
            "done",
            Data: JsonNode.Parse("{\"value\":42}"),
            MessageArguments: ["one"]
        );
        yield return [CommandPackage.CreateRequest(ResourceId.Parse("game:time"), [1, 2, 3], "typed")];
        yield return [CommandPackage.CreateResult(result, "result")];
        yield return
        [
            CommandPackage.CreatePermissionSnapshot(
                new PlayerAddress(IdentityAddress.Encode(new byte[32])),
                [new CommandPermissionGrant(ResourceId.Parse("game:time"), true)]
            )
        ];
        yield return [CommandPackage.CreateSuggestionRequest("time ", "suggest")];
        yield return
        [
            CommandPackage.CreateSuggestions(
                "time ",
                "suggest",
                [new CommandSuggestion("get", "fallback", false, new LocalizedText("test", "key", "fallback"))]
            )
        ];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryCommandModeRoundTripsWithoutRuntimeExecution(CommandPackage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()));
        var writer = ProtocolMessageCodec.Encode(message);
        var bytes = writer;
        var reader = bytes;
        var restored = Assert.IsAssignableFrom<CommandPackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(message.Mode, restored.Mode);
        Assert.Equal(message.CorrelationId, restored.CorrelationId);

        var repeated = ProtocolMessageCodec.Encode(restored);
        Assert.Equal(bytes, repeated);
    }

    [Fact]
    public void ParameterPayloadLimitsAreEnforcedBeforeReadingOrWriting()
    {
        const int maximumLength = ProtocolLimits.MaximumCommandPayloadLength;
        var message = new CommandRequestPackage
            { CommandId = ResourceId.Parse("game:time"), Payload = new byte[maximumLength] };
        var restored =
            Assert.IsType<CommandRequestPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message)));
        Assert.Equal(message.CommandId, restored.CommandId);
        Assert.Equal(message.Payload, restored.Payload);
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Encode(message with { Payload = new byte[maximumLength + 1] })
        );
        var oversized = MessagePack.MessagePackSerializer.Serialize(new byte[maximumLength + 1]);
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Decode(WireTestData.ReplaceField(message, 2, oversized))
        );
    }

    [Fact]
    public void ResponseContractDoesNotExposeGamePublishingPolicies()
    {
        var assembly = typeof(GameProtocol).Assembly;
        Assert.Null(assembly.GetType("Game.Protocol.Commands.CommandResult"));
        Assert.DoesNotContain(
            typeof(CommandResponse).GetProperties(),
            property =>
                property.Name is "Sensitive" or "Audience"
        );
    }

    [Fact]
    public void ResponsesCarryRequiredDataAndDisplayHints()
    {
        var response = new CommandResponse(
            true,
            "pending",
            "fallback",
            IsPending: true,
            MessageKey: "key",
            MessageArguments: ["value"],
            Data: new JsonObject { ["value"] = 42 },
            Presentation: GameMessagePresentation.History | GameMessagePresentation.Toast,
            CloseInputOnSuccess: true
        );
        var message = new CommandResultPackage { CorrelationId = "request", Result = response };
        var writer = ProtocolMessageCodec.Encode(message);
        var reader = writer;
        var restored = Assert.IsType<CommandResultPackage>(ProtocolMessageCodec.Decode(reader)).Result!;
        Assert.Equal(response.Success, restored.Success);
        Assert.Equal(response.Code, restored.Code);
        Assert.Equal(response.Message, restored.Message);
        Assert.True(restored.IsPending);
        Assert.Equal(response.MessageKey, restored.MessageKey);
        Assert.Equal(response.MessageArguments, restored.MessageArguments);
        Assert.Equal(42, restored.Data!["value"]!.GetValue<int>());
        Assert.Equal(
            response.Presentation,
            restored.Presentation
        );
        Assert.True(restored.CloseInputOnSuccess);
    }
}
