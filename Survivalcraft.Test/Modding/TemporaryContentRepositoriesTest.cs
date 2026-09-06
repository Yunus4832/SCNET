using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class TemporaryContentRepositoriesTest
{
    [Fact]
    public void AcceptsAndNormalizesBoundedAnonymousRepositories()
    {
        var repository = new ContentRepository
        {
            Name = "Session",
            BaseUrl = "https://content.example/base/",
            Priority = 10
        };

        var normalized = Assert.Single(TemporaryContentRepositories.Validate([repository]));

        Assert.Equal("https://content.example/base", normalized.BaseUrl);
    }

    [Fact]
    public void RejectsTooManyDuplicateOrUnsafeRepositories()
    {
        var tooMany = Enumerable.Range(0, TemporaryContentRepositories.MaximumCount + 1)
            .Select(index => new ContentRepository
            {
                Name = index.ToString(),
                BaseUrl = $"https://{index}.example",
                Priority = index
            });
        Assert.Throws<InvalidDataException>(() => TemporaryContentRepositories.Validate(tooMany));

        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        Assert.Throws<InvalidDataException>(() =>
            TemporaryContentRepositories.Validate([repository, repository with { Id = Guid.NewGuid() }]));
        Assert.Throws<InvalidDataException>(() => TemporaryContentRepositories.Validate([
            repository with { BaseUrl = "file:///tmp/content" }
        ]));
        Assert.Throws<InvalidDataException>(() => TemporaryContentRepositories.Validate([
            repository with { Priority = TemporaryContentRepositories.MaximumPriority + 1 }
        ]));
    }
}
