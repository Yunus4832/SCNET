using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class GroupManagePackage : IPackage
{
    public enum CommandType : byte
    {
        PromptJoinRequest,
        PromptInvitation,
        SyncGroups
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

    public Guid OperationId;

    public readonly List<GroupState> Groups = [];

    public Guid ToPlayer;

    public byte ID => (byte)PackageType.GroupManage;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public static GroupManagePackage CreatePrompt(
        SubsystemPlayers.PendingGroupOperation operation) =>
        new()
        {
            Command = operation.Kind is
                SubsystemPlayers.PendingGroupOperationKind.JoinRequest
                    ? CommandType.PromptJoinRequest
                    : CommandType.PromptInvitation,
            OperationId = operation.OperationId,
            FromPlayer = operation.Initiator,
            ToPlayer = operation.Responder,
            GroupKey = operation.GroupKey
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
            case CommandType.PromptJoinRequest:
            case CommandType.PromptInvitation:
                writer.Write(OperationId);
                writer.Write(FromPlayer);
                writer.Write(ToPlayer);
                writer.Write(GroupKey);
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
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Groups.Clear();
        Command = reader.ReadEnum<CommandType>();
        switch (Command)
        {
            case CommandType.PromptJoinRequest:
            case CommandType.PromptInvitation:
                OperationId = reader.ReadGuid();
                FromPlayer = reader.ReadGuid();
                ToPlayer = reader.ReadGuid();
                GroupKey = reader.ReadGuid();
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
        }
    }
}
