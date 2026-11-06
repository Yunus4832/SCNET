using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Replication;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class BodyInterestGroupTest
{
    [Fact]
    public void OwnerReceivesEntireMountedGroupWithoutAnIndexedLocation()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var node = WorldNetworkBinding.For(project).RequireNode();
        var owner = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        node.AddClient(owner);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(data, project);
        data.CharacterId = owner.CharacterId;
        var mount = new ComponentBody();
        var rider = new ComponentBody
        {
            ParentBody = mount,
            Character = new ComponentCharacter { CharacterData = data }
        };
        var interest = new SubsystemNetworkInterest();
        project.InitializeSubsystem(interest);

        Assert.Same(owner, Assert.Single(interest.GetBodyCandidates(mount)));
        Assert.Same(owner, Assert.Single(interest.GetBodyCandidates(rider)));
        rider.ParentBody = null;
        Assert.Empty(interest.GetBodyCandidates(mount));
        Assert.Same(owner, Assert.Single(interest.GetBodyCandidates(rider)));
    }

    [Fact]
    public void IndexedCandidatesIncludeAllMountedPositionsWithoutDuplicateConnections()
    {
        var interest = new SubsystemNetworkInterest();
        var index = (SpatialInterestIndex)typeof(SubsystemNetworkInterest)
            .GetField("_spatial", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(interest)!;
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var second = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        index.SetLocation(first, Vector2.Zero, 16);
        index.SetLocation(second, new Vector2(1024, 0), 16);
        var mount = new ComponentBody { Position = Vector3.Zero };
        var rider = new ComponentBody { Position = new Vector3(1024, 0, 0), ParentBody = mount };

        foreach (var member in new[] { mount, rider })
        {
            var candidates = interest.GetBodyCandidates(member).ToArray();
            Assert.Equal(2, candidates.Length);
            Assert.Contains(candidates, client => ReferenceEquals(client, first));
            Assert.Contains(candidates, client => ReferenceEquals(client, second));
        }

        rider.ParentBody = null;
        Assert.Same(first, Assert.Single(interest.GetBodyCandidates(mount)));
        Assert.Same(second, Assert.Single(interest.GetBodyCandidates(rider)));
        index.RetainClients([first]);
        Assert.Empty(interest.GetBodyCandidates(rider));
    }

    [Fact]
    public void EveryMountedMemberResolvesTheSameDependencyGroup()
    {
        var mount = new ComponentBody();
        var rider = new ComponentBody { ParentBody = mount };
        var passenger = new ComponentBody { ParentBody = mount };
        var nested = new ComponentBody { ParentBody = rider };

        foreach (var member in new[] { mount, rider, passenger, nested })
        {
            Assert.Equal(
                new[] { mount, rider, nested, passenger },
                SubsystemNetworkInterest.GetBodyGroup(member)
            );
        }
    }

    [Fact]
    public void DismountSeparatesDependencyGroups()
    {
        var mount = new ComponentBody();
        var rider = new ComponentBody { ParentBody = mount };

        rider.ParentBody = null;

        Assert.Equal([mount], SubsystemNetworkInterest.GetBodyGroup(mount));
        Assert.Equal([rider], SubsystemNetworkInterest.GetBodyGroup(rider));
    }
}
