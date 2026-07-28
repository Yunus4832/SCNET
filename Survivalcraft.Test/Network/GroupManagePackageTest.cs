using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class GroupManagePackageTest
{
    [Fact]
    public void ClientJoinRequestDoesNotCarryAnActorIdentity()
    {
        var groupKey = Guid.NewGuid();
        var package = GroupManagePackage.CreateJoinRequest(groupKey);

        var clone = RoundTrip(package);

        Assert.Equal(GroupManagePackage.CommandType.RequestJoinGroup, clone.Command);
        Assert.Equal(groupKey, clone.GroupKey);
        Assert.Equal(Guid.Empty, clone.FromPlayer);
        Assert.Equal(Guid.Empty, clone.OperationId);
    }

    [Fact]
    public void PendingOperationResponseRoundTrips()
    {
        var operationId = Guid.NewGuid();
        var package = GroupManagePackage.CreateResponse(operationId, true);

        var clone = RoundTrip(package);

        Assert.Equal(GroupManagePackage.CommandType.RespondRequest, clone.Command);
        Assert.Equal(operationId, clone.OperationId);
        Assert.True(clone.Result);
    }

    [Fact]
    public void ProtocolDoesNotExposeDirectJoinCommand()
    {
        Assert.DoesNotContain(
            "JoinGroup",
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
