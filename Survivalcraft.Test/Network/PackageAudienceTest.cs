using Game.Network;

namespace Survivalcraft.Test.Network;

public class PackageAudienceTest
{
    private readonly Client _first = CreateClient(1);

    private readonly Client _second = CreateClient(2);

    [Fact]
    public void GlobalIncludesEveryClient()
    {
        Assert.True(PackageAudience.Global.Includes(_first));
        Assert.True(PackageAudience.Global.Includes(_second));
    }

    [Fact]
    public void ClientAudienceIncludesOnlySelectedClient()
    {
        var audience = PackageAudience.To(_first);

        Assert.True(audience.Includes(_first));
        Assert.False(audience.Includes(_second));
    }

    [Fact]
    public void ExceptAudienceExcludesOnlySelectedClient()
    {
        var audience = PackageAudience.Except(_first);

        Assert.False(audience.Includes(_first));
        Assert.True(audience.Includes(_second));
    }

    [Fact]
    public void ClientSetIncludesOnlyMembers()
    {
        var third = CreateClient(3);
        var audience = PackageAudience.To([_first, _second]);

        Assert.True(audience.Includes(_first));
        Assert.True(audience.Includes(_second));
        Assert.False(audience.Includes(third));
    }

    private static Client CreateClient(byte id) =>
        new(null, id, Guid.NewGuid(), Guid.NewGuid(), null);

    [Fact]
    public void ClientSetCapturesRecipientsBeforeCallerReusesCollection()
    {
        var recipients = new List<Client> { _first };
        var audience = PackageAudience.To(recipients);

        recipients.Clear();
        recipients.Add(_second);

        Assert.True(audience.Includes(_first));
        Assert.False(audience.Includes(_second));
    }

    [Fact]
    public void ClientSetDoesNotIncludeReconnectedClientWithReusedId()
    {
        var audience = PackageAudience.To([_first, _first]);
        var reconnected = CreateClient(_first.ID);

        Assert.True(audience.Includes(_first));
        Assert.False(audience.Includes(reconnected));
    }

    [Fact]
    public void SingleRecipientAndExclusionUseConnectionIdentity()
    {
        var reconnected = CreateClient(_first.ID);

        Assert.False(PackageAudience.To(_first).Includes(reconnected));
        Assert.True(PackageAudience.Except(_first).Includes(reconnected));
    }
}
