using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Components;
using Game.Network;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class BodyInterestGroupTest
{
    [Fact]
    public void OwnerReceivesEntireMountedGroupWithoutAnIndexedLocation()
    {
        var owner = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var previousClients = CommonLib.Net.Clients;
        try
        {
            CommonLib.Net.Clients = new Dictionary<int, Client> { [owner.ID] = owner };
            var data = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
            data.PlayerGUID = owner.GUID;
            var mount = new ComponentBody();
            var rider = new ComponentBody
            {
                ParentBody = mount,
                Player = new ComponentPlayer { PlayerData = data }
            };
            var interest = new SubsystemNetworkInterest();

            Assert.Same(owner, Assert.Single(interest.GetBodyCandidates(mount)));
            Assert.Same(owner, Assert.Single(interest.GetBodyCandidates(rider)));
            rider.ParentBody = null;
            Assert.Empty(interest.GetBodyCandidates(mount));
            Assert.Same(owner, Assert.Single(interest.GetBodyCandidates(rider)));
        }
        finally
        {
            CommonLib.Net.Clients = previousClients;
        }
    }

    [Fact]
    public void IndexedCandidatesIncludeAllMountedPositionsWithoutDuplicateConnections()
    {
        var interest = new SubsystemNetworkInterest();
        var index = (SpatialInterestIndex)typeof(SubsystemNetworkInterest)
            .GetField("_spatial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(interest)!;
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var second = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
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
            Assert.Equal(new[] { mount, rider, nested, passenger },
                SubsystemNetworkInterest.GetBodyGroup(member));
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
