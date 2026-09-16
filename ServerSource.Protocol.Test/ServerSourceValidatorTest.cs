using ServerSource.Protocol;

namespace ServerSource.Protocol.Test;

public sealed class ServerSourceValidatorTest
{
    [Fact]
    public void ValidPagePassesValidation()
    {
        var result = ServerSourceValidator.Validate(CreatePage());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void DuplicateEntryIdFailsValidation()
    {
        var page = CreatePage() with
        {
            Servers =
            [
                CreateEntry("same", "one.example:28887"),
                CreateEntry("same", "two.example:28887")
            ]
        };

        var result = ServerSourceValidator.Validate(page);

        Assert.Contains(result.Issues, issue => issue.Code == ServerSourceValidationCode.DuplicateEntryId);
    }

    [Theory]
    [InlineData("example.org")]
    [InlineData("https://example.org:28887")]
    [InlineData("example.org:0")]
    [InlineData("example.org:65536")]
    [InlineData("user@example.org:28887")]
    public void InvalidServerAddressFailsValidation(string address)
    {
        var page = CreatePage() with { Servers = [CreateEntry("server", address)] };

        var result = ServerSourceValidator.Validate(page);

        Assert.Contains(result.Issues, issue => issue.Code == ServerSourceValidationCode.InvalidServerAddress);
    }

    [Theory]
    [InlineData("example.org:28887")]
    [InlineData("127.0.0.1:28887")]
    [InlineData("[2001:db8::1]:28887")]
    public void ExplicitServerAddressPassesValidation(string address)
    {
        Assert.True(ServerSourceValidator.IsValidServerAddress(address));
    }

    private static ServerSourcePage CreatePage()
    {
        return new ServerSourcePage(
            ServerSourceProtocol.CurrentVersion,
            new ServerSourceDescriptor("example-source", "Example Source"),
            [CreateEntry("server", "example.org:28887")],
            null);
    }

    private static ServerSourceEntry CreateEntry(string id, string address)
    {
        return new ServerSourceEntry(id, "Example Server", address, null, ["survival"]);
    }
}
