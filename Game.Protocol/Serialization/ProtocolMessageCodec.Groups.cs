using Game.Protocol.Players;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteGroupManagePackage(GroupManagePackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Command);
        switch (message.Command)
        {
            case GroupManagePackage.CommandType.PromptJoinRequest:
            case GroupManagePackage.CommandType.PromptInvitation:
                writer.Write(message.OperationId);
                writer.Write(message.FromPlayer);
                writer.Write(message.ToPlayer);
                writer.Write(message.GroupKey);
                break;
            case GroupManagePackage.CommandType.SyncGroups:
                if (message.Groups.Count > ushort.MaxValue) { throw new ArgumentException("Too many groups."); }
                writer.Write((ushort)message.Groups.Count);
                foreach (var group in message.Groups)
                {
                    writer.Write(group.GroupKey);
                    writer.Write(group.Name);
                    if (group.Members.Count > ushort.MaxValue) { throw new ArgumentException("Too many group members."); }
                    writer.Write((ushort)group.Members.Count);
                    foreach (var member in group.Members)
                    {
                        writer.Write(member);
                    }
                }

                break;
        }
    }

    private static GroupManagePackage ReadGroupManagePackage(ProtocolReader reader)
    {
        var command = reader.ReadEnum<GroupManagePackage.CommandType>();
        switch (command)
        {
            case GroupManagePackage.CommandType.PromptJoinRequest:
            case GroupManagePackage.CommandType.PromptInvitation:
                return new GroupManagePackage { Command = command, OperationId = reader.ReadGuid(), FromPlayer = reader.ReadGuid(), ToPlayer = reader.ReadGuid(), GroupKey = reader.ReadGuid() };
            case GroupManagePackage.CommandType.SyncGroups:
                var count = reader.ReadUInt16();
                EnsureRemaining(reader, count * 20);
                var groups = new GroupManagePackage.GroupState[count];
                for (var i = 0; i < count; i++)
                {
                    var key = reader.ReadGuid(); var name = reader.ReadString();
                    var length = reader.ReadUInt16(); EnsureRemaining(reader, length * 17);
                    var members = new Guid[length];
                    for (var j = 0; j < length; j++) { members[j] = reader.ReadGuid(); }
                    groups[i] = new GroupManagePackage.GroupState { GroupKey = key, Name = name, Members = members };
                }
                return new GroupManagePackage { Command = command, Groups = groups };
            default: throw new InvalidDataException("Unknown group message mode.");
        }
    }
}
