using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public class GroupManagePackageTest
{
    [Fact]
    public void PendingOperationPromptRoundTrips()
    {
        var operation = new SubsystemPlayers.PendingGroupOperation(
            Guid.NewGuid(),
            SubsystemPlayers.PendingGroupOperationKind.JoinRequest,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            60);
        var package = GroupManagePackage.CreatePrompt(operation);

        var clone = RoundTrip(package);

        Assert.Equal(GroupManagePackage.CommandType.PromptJoinRequest, clone.Command);
        Assert.Equal(operation.OperationId, clone.OperationId);
        Assert.Equal(operation.Initiator, clone.FromPlayer);
        Assert.Equal(operation.Responder, clone.ToPlayer);
        Assert.Equal(operation.GroupKey, clone.GroupKey);
    }

    [Fact]
    public void ProtocolContainsOnlyServerAuthoredEvents()
    {
        Assert.Equal(
            [
                nameof(GroupManagePackage.CommandType.PromptJoinRequest),
                nameof(GroupManagePackage.CommandType.PromptInvitation),
                nameof(GroupManagePackage.CommandType.SyncGroups)
            ],
            Enum.GetNames<GroupManagePackage.CommandType>());
    }

    [Fact]
    public void GroupSnapshotRoundTrips()
    {
        var groupKey = Guid.NewGuid();
        var firstMember = Guid.NewGuid();
        var secondMember = Guid.NewGuid();
        var package = new GroupManagePackage
        {
            Command = GroupManagePackage.CommandType.SyncGroups
        };
        var group = new GroupManagePackage.GroupState
        {
            GroupKey = groupKey,
            Name = "Builders"
        };
        group.Members.Add(firstMember);
        group.Members.Add(secondMember);
        package.Groups.Add(group);

        var clone = RoundTrip(package);

        Assert.Equal(GroupManagePackage.CommandType.SyncGroups, clone.Command);
        var clonedGroup = Assert.Single(clone.Groups);
        Assert.Equal(groupKey, clonedGroup.GroupKey);
        Assert.Equal("Builders", clonedGroup.Name);
        Assert.Equal([firstMember, secondMember], clonedGroup.Members);
    }

    private static GroupManagePackage RoundTrip(GroupManagePackage package)
    {
        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new GroupManagePackage();
        clone.ReadData(reader);
        return clone;
    }
}
