using Engine.Core;

using Game.Network.Serialization;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

public class TerritoryAccessTest
{
    [Theory]
    [InlineData("enter,dig", TerritoryPermission.Enter | TerritoryPermission.Dig)]
    [InlineData("all", TerritoryPermission.All)]
    [InlineData("none", TerritoryPermission.None)]
    public void PermissionSyntaxIsExplicit(string text, TerritoryPermission expected)
    {
        Assert.True(TerritoryPermissions.TryParse(text, out var permissions));
        Assert.Equal(expected, permissions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("enter,,dig")]
    [InlineData("16")]
    [InlineData("enter,unknown")]
    public void InvalidPermissionsAreRejected(string text) => Assert.False(TerritoryPermissions.TryParse(text, out _));

    [Fact]
    public void RequestsCanBeRepeatedAfterRejectionAndCooldown()
    {
        var requests = new TerritoryAccessRequests();
        var owner = Guid.NewGuid();
        var player = Guid.NewGuid();
        Assert.True(requests.TryAdd(owner, player, "Player", 100, out var request, out _));
        Assert.False(requests.TryAdd(owner, player, "Player", 101, out _, out var duplicate));
        Assert.Equal("AlreadyPending", duplicate);
        Assert.True(requests.TryResolve(request!.Id, owner, TerritoryPermission.None, 102, out _));
        Assert.False(requests.TryAdd(owner, player, "Player", 103, out _, out var cooldown));
        Assert.Equal("RateLimited", cooldown);
        Assert.True(requests.TryAdd(owner, player, "Player", 130, out _, out _));
    }

    [Fact]
    public void OnlyOwnerCanChoosePermissionsAndApproveOnlyOnce()
    {
        var requests = new TerritoryAccessRequests();
        var owner = Guid.NewGuid();
        Assert.True(requests.TryAdd(owner, Guid.NewGuid(), "Player",
            10, out var request, out _));
        Assert.False(requests.TryResolve(request!.Id, Guid.NewGuid(), TerritoryPermission.Dig, 11, out _));
        Assert.False(requests.TryResolve(request.Id, owner, (TerritoryPermission)128, 11, out _));
        Assert.True(requests.TryResolve(request.Id, owner, TerritoryPermission.All, 11, out var resolved));
        Assert.Equal(request, resolved);
        Assert.False(requests.TryResolve(request.Id, owner, TerritoryPermission.Dig, 12, out _));
    }

    [Fact]
    public void ExpiredOrDisconnectedRequestsAreRemovedWithoutSaving()
    {
        var requests = new TerritoryAccessRequests();
        var owner = Guid.NewGuid();
        requests.TryAdd(owner, Guid.NewGuid(), "First", 0, out var first, out _);
        requests.TryAdd(owner, Guid.NewGuid(), "Second", 10, out var second, out _);
        Assert.False(requests.TryResolve(first!.Id, owner, TerritoryPermission.Dig, 120, out _));
        var removed = requests.Expire(120, request => request.Id == second!.Id);
        Assert.Equal(2, removed.Length);
        Assert.Empty(requests.Pending);
    }

    [Fact]
    public void PerOwnerQueueIsBounded()
    {
        var requests = new TerritoryAccessRequests();
        var owner = Guid.NewGuid();
        for (var index = 0; index < 8; index++)
        {
            Assert.True(requests.TryAdd(owner, Guid.NewGuid(), "Player", 0, out _, out _));
        }
        Assert.False(requests.TryAdd(owner, Guid.NewGuid(), "Player", 0, out _, out var error));
        Assert.Equal("QueueFull", error);
    }

    [Fact]
    public void GrantsSurviveDiskAndNetworkRoundTripsAndRevocation()
    {
        var owner = Guid.NewGuid();
        var player = Guid.NewGuid();
        var store = new TerritoryStore();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.True(store.TryGet(owner, out var territory));
        territory!.SetGrant(player, "Visitor", TerritoryPermission.Enter | TerritoryPermission.Dig);
        var restored = new TerritoryStore();
        restored.Load(store.Save());
        var saved = Assert.Single(restored.Territories);
        Assert.Equal(territory.Grants[player], saved.Grants[player]);
        using var writer = new PackageStreamWriter();
        TerritoryMessages.Write(writer, saved);
        using var reader = new PackageStreamReader(writer.Data());
        var received = TerritoryMessages.Read(reader);
        Assert.Equal(saved.Grants[player], received.Grants[player]);
        received.SetGrant(player, "Visitor", TerritoryPermission.None);
        Assert.Empty(received.Grants);
        Assert.Equal(TerritoryPermission.None, received.PermissionsFor(player));
    }

    [Fact]
    public void GrantsRejectInvalidIdentityAndUnknownPermissionBits()
    {
        var territory = new Territory(Guid.NewGuid(), new Point3(0, 64, 0));
        Assert.Throws<InvalidDataException>(() => territory.SetGrant(Guid.Empty, "Player", TerritoryPermission.Enter));
        Assert.Throws<InvalidDataException>(() => territory.SetGrant(territory.Owner, "Owner", TerritoryPermission.All));
        Assert.Throws<InvalidDataException>(() => territory.SetGrant(Guid.NewGuid(), "Player", (TerritoryPermission)16));
    }
}
