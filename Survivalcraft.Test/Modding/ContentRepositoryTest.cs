using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentRepositoryTest
{
    [Fact]
    public void NormalizePreservesIdentityAndPathPrefix()
    {
        var original = new ContentRepository { Name = " A ", BaseUrl = " https://EXAMPLE.com:443/content/ " };
        var normalized = original.Normalize();
        Assert.Equal(original.Id, normalized.Id);
        Assert.Equal("A", normalized.Name);
        Assert.Equal("https://example.com/content", normalized.BaseUrl);
        Assert.Equal(" https://EXAMPLE.com:443/content/ ", original.BaseUrl);
    }

    [Theory]
    [InlineData("file:///tmp/packages")]
    [InlineData("/packages")]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://example.com/?token=value")]
    [InlineData("https://example.com/#fragment")]
    public void RejectsInvalidRepositoryAddresses(string address)
    {
        Assert.Throws<ArgumentException>(() => new ContentRepository { Name = "A", BaseUrl = address }.Normalize());
    }

    [Fact]
    public void RejectsEquivalentAddressesWithoutChangingInput()
    {
        var first = new ContentRepository { Name = "A", BaseUrl = "https://EXAMPLE.com:443/" };
        var second = new ContentRepository { Name = "B", BaseUrl = "https://example.com" };
        Assert.Throws<ArgumentException>(() => ContentRepository.NormalizeAll([first, second]));
        Assert.Throws<ArgumentException>(() => ContentRepository.NormalizeAll([first, first with { BaseUrl = "https://other.example" }]));
    }

    [Fact]
    public void OrdersByPriorityThenStableIdIncludingDisabledEntries()
    {
        var first = new ContentRepository { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "A", BaseUrl = "https://a.example", Priority = 1 };
        var second = new ContentRepository { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "B", BaseUrl = "https://b.example", Priority = 1 };
        var disabled = new ContentRepository { Name = "C", BaseUrl = "https://c.example", Priority = 0, IsEnabled = false };
        Assert.Equal(new[] { disabled.Id, first.Id, second.Id }, ContentRepository.NormalizeAll([second, first, disabled]).Select(item => item.Id));
    }
}
