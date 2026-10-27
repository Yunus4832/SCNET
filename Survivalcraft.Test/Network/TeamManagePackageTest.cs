using Engine.Core.Security;

using Game.Protocol.Identity;
using Game.Teams;

namespace Survivalcraft.Test.Network;

public class TeamManagePackageTest
{
    [Fact]
    public void PendingOperationPromptRoundTrips()
    {
        var operation = new TeamOperation(
            Guid.NewGuid(),
            TeamOperationKind.JoinRequest,
            Address(1),
            Address(2),
            Address(1),
            Guid.NewGuid(),
            60
        );
        var package = TeamPackageBuilder.CreatePrompt(operation);

        var clone = RoundTrip(package);

        Assert.Equal(TeamManagePackage.CommandType.PromptJoinRequest, clone.Command);
        Assert.Equal(operation.Id, clone.OperationId);
        Assert.Equal(operation.Initiator, clone.Initiator);
        Assert.Equal(operation.Responder, clone.Responder);
        Assert.Equal(operation.TeamId, clone.TeamId);
    }

    [Fact]
    public void ProtocolContainsOnlyServerAuthoredEvents()
    {
        Assert.Equal(
            [
                nameof(TeamManagePackage.CommandType.PromptJoinRequest),
                nameof(TeamManagePackage.CommandType.PromptInvitation),
                nameof(TeamManagePackage.CommandType.SyncTeams)
            ],
            Enum.GetNames<TeamManagePackage.CommandType>()
        );
    }

    [Fact]
    public void GroupSnapshotRoundTrips()
    {
        var groupKey = Guid.NewGuid();
        var firstMember = Address(1);
        var secondMember = Address(2);
        var package = new TeamSnapshotPackage
        {
            Teams =
            [
                new TeamManagePackage.TeamState
                {
                    TeamId = groupKey, Name = "Builders", Leader = firstMember, Members = [firstMember, secondMember]
                }
            ]
        };

        var clone = RoundTrip(package);

        Assert.Equal(TeamManagePackage.CommandType.SyncTeams, clone.Command);
        var clonedGroup = Assert.Single(clone.Teams);
        Assert.Equal(groupKey, clonedGroup.TeamId);
        Assert.Equal("Builders", clonedGroup.Name);
        Assert.Equal(firstMember, clonedGroup.Leader);
        Assert.Equal([firstMember, secondMember], clonedGroup.Members);
    }

    private static PlayerAddress Address(byte value)
    {
        var bytes = new byte[32];
        bytes[0] = value;
        return new PlayerAddress(IdentityAddress.Encode(bytes));
    }

    private static T RoundTrip<T>(T package) where T : TeamManagePackage
    {
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsAssignableFrom<T>(ProtocolMessageCodec.Decode(reader));
        return clone;
    }
}
