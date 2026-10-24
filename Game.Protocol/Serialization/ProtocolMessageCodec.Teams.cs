using Engine.Core.Security;

using Game.Protocol.Identity;
using Game.Protocol.Teams;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteTeamManagePackage(TeamManagePackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Command);
        switch (message.Command)
        {
            case TeamManagePackage.CommandType.PromptJoinRequest:
            case TeamManagePackage.CommandType.PromptInvitation:
                writer.Write(message.OperationId);
                PlayerAddressCodec.Write(writer, message.Initiator);
                PlayerAddressCodec.Write(writer, message.Responder);
                writer.Write(message.TeamId);
                break;
            case TeamManagePackage.CommandType.SyncTeams:
                if (message.Teams.Count > ushort.MaxValue) { throw new ArgumentException("Too many groups."); }

                writer.Write((ushort)message.Teams.Count);
                foreach (var group in message.Teams)
                {
                    writer.Write(group.TeamId);
                    writer.Write(group.Name);
                    PlayerAddressCodec.Write(writer, group.Leader);
                    if (group.Members.Count > ushort.MaxValue)
                    {
                        throw new ArgumentException("Too many group members.");
                    }

                    writer.Write((ushort)group.Members.Count);
                    foreach (var member in group.Members)
                    {
                        PlayerAddressCodec.Write(writer, member);
                    }
                }

                break;
        }
    }

    private static TeamManagePackage ReadTeamManagePackage(ProtocolReader reader)
    {
        var command = reader.ReadEnum<TeamManagePackage.CommandType>();
        switch (command)
        {
            case TeamManagePackage.CommandType.PromptJoinRequest:
            case TeamManagePackage.CommandType.PromptInvitation:
                return new TeamManagePackage
                {
                    Command = command, OperationId = reader.ReadGuid(),
                    Initiator = PlayerAddressCodec.Read(reader),
                    Responder = PlayerAddressCodec.Read(reader), TeamId = reader.ReadGuid()
                };
            case TeamManagePackage.CommandType.SyncTeams:
                var count = reader.ReadUInt16();
                EnsureRemaining(reader, count * (16 + 1 + IdentityAddress.EncodedLength + 1 + 2));
                var groups = new TeamManagePackage.TeamState[count];
                for (var i = 0; i < count; i++)
                {
                    var key = reader.ReadGuid();
                    var name = reader.ReadString();
                    var leader = PlayerAddressCodec.Read(reader);
                    var length = reader.ReadUInt16();
                    EnsureRemaining(reader, length * (IdentityAddress.EncodedLength + 1));
                    var members = new PlayerAddress[length];
                    for (var j = 0; j < length; j++) { members[j] = PlayerAddressCodec.Read(reader); }

                    groups[i] = new TeamManagePackage.TeamState
                        { TeamId = key, Name = name, Leader = leader, Members = members };
                }

                return new TeamManagePackage { Command = command, Teams = groups };
            default: throw new InvalidDataException("Unknown group message mode.");
        }
    }
}
