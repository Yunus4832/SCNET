using Game.Network;
using Game.Network.Transport;

namespace Survivalcraft.Core.Test.Network;

public class PackageAudienceTest
{
    private readonly WorldConnection _first = CreateClient(1);

    private readonly WorldConnection _second = CreateClient(2);

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

    private static WorldConnection CreateClient(byte id) =>
        new(id, Guid.NewGuid());

    [Fact]
    public void ClientSetCapturesRecipientsBeforeCallerReusesCollection()
    {
        var recipients = new List<WorldConnection> { _first };
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
