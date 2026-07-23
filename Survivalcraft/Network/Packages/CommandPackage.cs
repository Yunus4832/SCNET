using Game.Commands;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class CommandPackage : IPackage
{
    public enum CommandPackageMode : byte
    {
        Request,
        Result,
        PermissionSnapshot
    }

    private const int _maximumPermissionCount = 256;

    public byte ID => (byte)PackageType.Command;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public CommandPackageMode Mode { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string Input { get; private set; } = string.Empty;

    public bool Success { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public Guid PlayerGuid { get; private set; }

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

    public static CommandPackage CreateResult(string correlationId, bool success, string code, string message)
    {
        return new CommandPackage(CommandPackageMode.Result, correlationId)
        {
            Success = success,
            Code = code,
            Message = message
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

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Mode);
        writer.Write(CorrelationId);
        switch (Mode)
        {
            case CommandPackageMode.Request:
                writer.Write(Input);
                break;
            case CommandPackageMode.Result:
                writer.Write(Success);
                writer.Write(Code);
                writer.Write(Message);
                break;
            case CommandPackageMode.PermissionSnapshot:
                writer.Write(PlayerGuid);
                writer.Write((ushort)PermissionGrants.Count);
                foreach (var grant in PermissionGrants)
                {
                    writer.Write(grant.Permission);
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
            case CommandPackageMode.Result:
                Success = reader.ReadBoolean();
                Code = reader.ReadString();
                Message = reader.ReadString();
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
                        reader.ReadString(),
                        reader.ReadBoolean());
                }

                PermissionGrants = grants;
                break;
        }
    }
}
