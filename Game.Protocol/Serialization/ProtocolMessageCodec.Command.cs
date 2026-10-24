using System.Text.Json.Nodes;

using Game.Protocol.Commands;
using Game.Protocol.Localization;
using Game.Protocol.Messaging;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private const int _maximumPermissionCount = 256;

    private const int _maximumMessageArgumentCount = 32;

    private static void WriteCommandPackage(CommandPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Mode);
        writer.Write(message.CorrelationId);
        switch (message.Mode)
        {
            case CommandPackage.CommandPackageMode.SuggestionRequest:
                writer.Write(message.Input);
                break;
            case CommandPackage.CommandPackageMode.Suggestions:
                writer.Write(message.Input);
                if (message.Suggestions.Count > 64) { throw new ArgumentException("Too many command suggestions."); }

                writer.Write((byte)message.Suggestions.Count);
                foreach (var suggestion in message.Suggestions)
                {
                    writer.Write(suggestion.Value);
                    writer.Write(suggestion.IsArgument);
                    var description = suggestion.DescriptionSource ?? LocalizedText.Literal(suggestion.Description);
                    writer.Write(description.Section);
                    writer.Write(description.Key);
                    writer.Write(description.Fallback);
                }

                break;
            case CommandPackage.CommandPackageMode.Request:
                writer.Write(message.CommandId.Namespace.Value);
                writer.Write(message.CommandId.Path);
                var maximumPayloadLength = ProtocolLimits.MaximumCommandPayloadLength;
                if (message.Payload.Length > maximumPayloadLength)
                {
                    throw new ArgumentException("Command payload is too long.");
                }

                writer.WriteBuff(message.Payload);
                break;
            case CommandPackage.CommandPackageMode.Result:
                var result = message.Result ??
                             throw new InvalidOperationException("Command result package has no result.");
                writer.Write(result.Success);
                writer.Write(result.Code);
                writer.Write(result.Message);
                writer.Write(result.IsPending);
                writer.WriteEnum(result.Presentation);
                writer.Write(result.CloseInputOnSuccess);
                writer.Write(result.MessageKey);
                var arguments = result.MessageArguments ?? [];
                if (arguments.Count > _maximumMessageArgumentCount)
                {
                    throw new InvalidOperationException("Command result contains too many localization arguments.");
                }

                writer.Write((byte)arguments.Count);
                foreach (var argument in arguments)
                {
                    writer.Write(argument);
                }

                writer.Write(result.Data?.ToJsonString() ?? string.Empty);
                break;
            case CommandPackage.CommandPackageMode.PermissionSnapshot:
                PlayerAddressCodec.Write(
                    writer,
                    message.PlayerAddress ?? throw new ArgumentException("A subject address is required.")
                );
                if (message.PermissionGrants.Count > _maximumPermissionCount)
                {
                    throw new ArgumentException("Too many command permissions.");
                }

                writer.Write((ushort)message.PermissionGrants.Count);
                foreach (var grant in message.PermissionGrants)
                {
                    writer.Write(grant.Permission.Namespace.Value);
                    writer.Write(grant.Permission.Path);
                    writer.Write(grant.CanDelegate);
                }

                break;
        }
    }

    private static CommandPackage ReadCommandPackage(ProtocolReader reader)
    {
        CommandPackage.CommandPackageMode Mode = default;
        string CorrelationId = string.Empty, Input = string.Empty;
        ResourceId CommandId = default;
        byte[] Payload = [];
        Game.Protocol.Identity.PlayerAddress? PlayerAddress = null;
        CommandResponse? Result = null;
        IReadOnlyList<CommandPermissionGrant> PermissionGrants = [];
        IReadOnlyList<CommandSuggestion> Suggestions = [];

        Mode = reader.ReadEnum<CommandPackage.CommandPackageMode>();
        CorrelationId = reader.ReadString();
        switch (Mode)
        {
            case CommandPackage.CommandPackageMode.SuggestionRequest:
                Input = reader.ReadString();
                break;
            case CommandPackage.CommandPackageMode.Suggestions:
                Input = reader.ReadString();
                var suggestionCount = reader.ReadByte();
                if (suggestionCount > 64)
                {
                    throw new InvalidDataException("Too many command suggestions.");
                }

                var suggestions = new CommandSuggestion[suggestionCount];
                for (var index = 0; index < suggestionCount; index++)
                {
                    var value = reader.ReadString();
                    var isArgument = reader.ReadBoolean();
                    var section = reader.ReadString();
                    var key = reader.ReadString();
                    var fallback = reader.ReadString();
                    var description = section.Length == 0
                        ? LocalizedText.Literal(fallback)
                        : new LocalizedText(section, key, fallback);
                    suggestions[index] = new CommandSuggestion(value, fallback, isArgument, description);
                }

                Suggestions = suggestions;
                break;
            case CommandPackage.CommandPackageMode.Request:
                CommandId = new ResourceId(
                    new ModId(reader.ReadString()),
                    reader.ReadString()
                );
                var payloadLength = reader.ReadInt32();
                var maximumPayloadLength = ProtocolLimits.MaximumCommandPayloadLength;
                if (payloadLength < 0 || payloadLength > maximumPayloadLength)
                {
                    throw new InvalidDataException($"Command payload length is invalid: {payloadLength}.");
                }

                Payload = reader.ReadBytes(payloadLength);
                if (Payload.Length != payloadLength)
                {
                    throw new EndOfStreamException("Command payload is truncated.");
                }

                break;
            case CommandPackage.CommandPackageMode.Result:
                var success = reader.ReadBoolean();
                var code = reader.ReadString();
                var message = reader.ReadString();
                var pending = reader.ReadBoolean();
                var presentation = reader.ReadEnum<GameMessagePresentation>();
                var closeInputOnSuccess = reader.ReadBoolean();
                var messageKey = reader.ReadString();
                var argumentCount = reader.ReadByte();
                if (argumentCount > _maximumMessageArgumentCount)
                {
                    throw new InvalidDataException("Command result contains too many localization arguments.");
                }

                var messageArguments = new string[argumentCount];
                for (var index = 0; index < argumentCount; index++)
                {
                    messageArguments[index] = reader.ReadString();
                }

                var dataText = reader.ReadString();
                var data = string.IsNullOrEmpty(dataText) ? null : JsonNode.Parse(dataText);
                Result = new CommandResponse(
                    success,
                    code,
                    message,
                    IsPending: pending,
                    MessageKey: messageKey,
                    MessageArguments: messageArguments,
                    Data: data,
                    Presentation: presentation,
                    CloseInputOnSuccess: closeInputOnSuccess
                );
                break;
            case CommandPackage.CommandPackageMode.PermissionSnapshot:
                PlayerAddress = PlayerAddressCodec.Read(reader);
                var count = reader.ReadUInt16();
                if (count > _maximumPermissionCount)
                {
                    throw new InvalidDataException($"Too many command permissions: {count}.");
                }

                var grants = new CommandPermissionGrant[count];
                for (var index = 0; index < count; index++)
                {
                    grants[index] = new CommandPermissionGrant(
                        new ResourceId(
                            new ModId(reader.ReadString()),
                            reader.ReadString()
                        ),
                        reader.ReadBoolean()
                    );
                }

                PermissionGrants = grants;
                break;
        }

        if (!Enum.IsDefined(Mode)) { throw new InvalidDataException("Unknown command message mode."); }

        return new CommandPackage(Mode, CorrelationId)
        {
            Input = Input, CommandId = CommandId, Payload = Payload, PlayerAddress = PlayerAddress, Result = Result,
            PermissionGrants = PermissionGrants, Suggestions = Suggestions
        };
    }
}
