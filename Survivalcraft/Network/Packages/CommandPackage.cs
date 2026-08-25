using Game.Commands;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class CommandPackage : IPackage
{
    public enum CommandPackageMode : byte
    {
        Request,
        TypedRequest,
        Result,
        PermissionSnapshot
    }

    private const int _maximumPermissionCount = 256;

    private const int _maximumMessageArgumentCount = 32;

    public byte ID => (byte)PackageType.Command;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public CommandPackageMode Mode { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string Input { get; private set; } = string.Empty;

    public ResourceId CommandId { get; private set; }

    public byte[] Payload { get; private set; } = [];

    public Guid PlayerGuid { get; private set; }

    public CommandResult? Result { get; private set; }

    public IReadOnlyList<CommandPermissionGrant> PermissionGrants { get; private set; } = [];

    public CommandPackage()
    {
    }

    private CommandPackage(CommandPackageMode mode, string correlationId)
    {
        Mode = mode;
        CorrelationId = correlationId;
    }

    public static CommandPackage CreateRequest(string input, string? correlationId = null)
    {
        return new CommandPackage(
            CommandPackageMode.Request,
            correlationId ?? Guid.NewGuid().ToString("N"))
        {
            Input = input
        };
    }

    public static CommandPackage CreateRequest(
        ResourceId commandId,
        byte[] payload,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new CommandPackage(
            CommandPackageMode.TypedRequest,
            correlationId ?? Guid.NewGuid().ToString("N"))
        {
            CommandId = commandId,
            Payload = payload
        };
    }

    public static CommandPackage CreatePermissionSnapshot(
        Guid playerGuid,
        IEnumerable<CommandPermissionGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        return new CommandPackage(CommandPackageMode.PermissionSnapshot, string.Empty)
        {
            PlayerGuid = playerGuid,
            PermissionGrants = grants.Take(_maximumPermissionCount).ToArray()
        };
    }

    public static CommandPackage CreateResult(
        CommandResult result,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new CommandPackage(CommandPackageMode.Result, correlationId)
        {
            Result = result
        };
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Mode);
        writer.Write(CorrelationId);
        switch (Mode)
        {
            case CommandPackageMode.Request:
                writer.Write(Input);
                break;
            case CommandPackageMode.TypedRequest:
                writer.Write(CommandId.Namespace.Value);
                writer.Write(CommandId.Path);
                writer.WriteBuff(Payload);
                break;
            case CommandPackageMode.Result:
                var result = Result ??
                             throw new InvalidOperationException(
                                 "Command result package has no result.");
                writer.Write(result.Success);
                writer.Write(result.Code);
                writer.Write(result.Message);
                writer.Write(result.Sensitive);
                writer.WriteEnum(result.Audience);
                writer.WriteEnum(result.State);
                writer.WriteEnum(result.Presentation);
                writer.Write(result.MessageKey);
                var arguments = result.MessageArguments ?? [];
                if (arguments.Count > _maximumMessageArgumentCount)
                {
                    throw new InvalidOperationException(
                        "Command result contains too many localization arguments.");
                }

                writer.Write((byte)arguments.Count);
                foreach (var argument in arguments)
                {
                    writer.Write(argument);
                }

                break;
            case CommandPackageMode.PermissionSnapshot:
                writer.Write(PlayerGuid);
                writer.Write((ushort)PermissionGrants.Count);
                foreach (var grant in PermissionGrants)
                {
                    writer.Write(grant.Permission.Namespace.Value);
                    writer.Write(grant.Permission.Path);
                    writer.Write(grant.CanDelegate);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Mode = reader.ReadEnum<CommandPackageMode>();
        CorrelationId = reader.ReadString();
        switch (Mode)
        {
            case CommandPackageMode.Request:
                Input = reader.ReadString();
                break;
            case CommandPackageMode.TypedRequest:
                CommandId = new ResourceId(
                    new ModId(reader.ReadString()),
                    reader.ReadString());
                var payloadLength = reader.ReadInt32();
                if (payloadLength is < 0 or > 4096)
                {
                    throw new InvalidDataException(
                        $"Command payload length is invalid: {payloadLength}.");
                }

                Payload = reader.ReadBytes(payloadLength);
                if (Payload.Length != payloadLength)
                {
                    throw new EndOfStreamException("Command payload is truncated.");
                }

                break;
            case CommandPackageMode.Result:
                var success = reader.ReadBoolean();
                var code = reader.ReadString();
                var message = reader.ReadString();
                var sensitive = reader.ReadBoolean();
                var audience = reader.ReadEnum<CommandResultAudience>();
                var state = reader.ReadEnum<CommandResultState>();
                var presentation = reader.ReadEnum<CommandResultPresentation>();
                var messageKey = reader.ReadString();
                var argumentCount = reader.ReadByte();
                if (argumentCount > _maximumMessageArgumentCount)
                {
                    throw new InvalidDataException(
                        "Command result contains too many localization arguments.");
                }

                var messageArguments = new string[argumentCount];
                for (var index = 0; index < argumentCount; index++)
                {
                    messageArguments[index] = reader.ReadString();
                }

                Result = new CommandResult(
                    success,
                    code,
                    message,
                    sensitive,
                    audience,
                    state,
                    presentation,
                    messageKey,
                    messageArguments);
                break;
            case CommandPackageMode.PermissionSnapshot:
                PlayerGuid = reader.ReadGuid();
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
                            reader.ReadString()),
                        reader.ReadBoolean());
                }

                PermissionGrants = grants;
                break;
        }
    }
}
