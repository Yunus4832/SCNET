using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class GroupManagePackage : IPackage
{
    public enum CommandType : byte
    {
        CreateGroup,
        RequestJoinGroup,
        InviteJoinGroup,
        RespondRequest,
        ExitGroup,
        SyncGroups,
        OperationResult
    }

    public sealed class GroupState
    {
        public Guid GroupKey;

        public string Name = string.Empty;

        public readonly List<Guid> Members = [];
    }

    public CommandType Command;

    public Guid FromPlayer;

    public Guid GroupKey;

    public string GroupName = string.Empty;

    public string Message = string.Empty;

    public Guid OperationId;

    public readonly List<GroupState> Groups = [];

    public bool Result;

    public Guid ToPlayer;

    public byte ID => (byte)PackageType.GroupManage;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public static GroupManagePackage CreateGroupRequest(string groupName) =>
        new()
        {
            Command = CommandType.CreateGroup,
            GroupName = groupName
        };

    public static GroupManagePackage CreateJoinRequest(Guid groupKey) =>
        new()
        {
            Command = CommandType.RequestJoinGroup,
            GroupKey = groupKey
        };

    public static GroupManagePackage CreateInvitation(Guid groupKey, Guid targetPlayer) =>
        new()
        {
            Command = CommandType.InviteJoinGroup,
            GroupKey = groupKey,
            ToPlayer = targetPlayer
        };

    public static GroupManagePackage CreateResponse(Guid operationId, bool accepted) =>
        new()
        {
            Command = CommandType.RespondRequest,
            OperationId = operationId,
            Result = accepted
        };

    public static GroupManagePackage CreateExitRequest() =>
        new() { Command = CommandType.ExitGroup };

    public static GroupManagePackage CreateResult(
        bool result,
        string message,
        Guid operationId = default) =>
        new()
        {
            Command = CommandType.OperationResult,
            OperationId = operationId,
            Result = result,
            Message = message
        };

    public static GroupManagePackage CreateSnapshot(SubsystemPlayers subsystemPlayers)
    {
        var package = new GroupManagePackage { Command = CommandType.SyncGroups };
        foreach (var item in subsystemPlayers.ServerGroups)
        {
            if (!Guid.TryParse(item.Key, out var groupKey))
            {
                throw new InvalidOperationException($"Invalid group key '{item.Key}'.");
            }

            var group = new GroupState
            {
                GroupKey = groupKey,
                Name = item.Value.Name
            };
            group.Members.AddRange(item.Value.Members);
            package.Groups.Add(group);
        }

        return package;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Command);
        switch (Command)
        {
            case CommandType.CreateGroup:
                writer.Write(GroupName);
                break;
            case CommandType.RequestJoinGroup:
            case CommandType.InviteJoinGroup:
                writer.Write(OperationId);
                writer.Write(FromPlayer);
                writer.Write(ToPlayer);
                writer.Write(GroupKey);
                break;
            case CommandType.RespondRequest:
                writer.Write(OperationId);
                writer.Write(Result);
                break;
            case CommandType.ExitGroup:
                break;
            case CommandType.SyncGroups:
                writer.Write((ushort)Groups.Count);
                foreach (var group in Groups)
                {
                    writer.Write(group.GroupKey);
                    writer.Write(group.Name);
                    writer.Write((ushort)group.Members.Count);
                    foreach (var member in group.Members)
                    {
                        writer.Write(member);
                    }
                }

                break;
            case CommandType.OperationResult:
                writer.Write(OperationId);
                writer.Write(Result);
                writer.Write(Message);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Groups.Clear();
        Command = reader.ReadEnum<CommandType>();
        switch (Command)
        {
            case CommandType.CreateGroup:
                GroupName = reader.ReadString();
                break;
            case CommandType.RequestJoinGroup:
            case CommandType.InviteJoinGroup:
                OperationId = reader.ReadGuid();
                FromPlayer = reader.ReadGuid();
                ToPlayer = reader.ReadGuid();
                GroupKey = reader.ReadGuid();
                break;
            case CommandType.RespondRequest:
                OperationId = reader.ReadGuid();
                Result = reader.ReadBoolean();
                break;
            case CommandType.ExitGroup:
                break;
            case CommandType.SyncGroups:
                var groupCount = reader.ReadUInt16();
                for (var i = 0; i < groupCount; i++)
                {
                    var group = new GroupState
                    {
                        GroupKey = reader.ReadGuid(),
                        Name = reader.ReadString()
                    };
                    var memberCount = reader.ReadUInt16();
                    for (var j = 0; j < memberCount; j++)
                    {
                        group.Members.Add(reader.ReadGuid());
                    }

                    Groups.Add(group);
                }

                break;
            case CommandType.OperationResult:
                OperationId = reader.ReadGuid();
                Result = reader.ReadBoolean();
                Message = reader.ReadString();
                break;
        }
    }
}
