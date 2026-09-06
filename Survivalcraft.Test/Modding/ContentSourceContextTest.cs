using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentSourceContextTest
{
    [Fact]
    public void SessionSourcesPrecedePersistentSourcesAndWinAddressDeduplication()
    {
        var duplicate = new ContentRepository
        {
            Name = "Persistent duplicate",
            BaseUrl = "https://same.example",
            Priority = 0
        };
        var persistent = new ContentRepository
        {
            Name = "Persistent",
            BaseUrl = "https://persistent.example",
            Priority = 1
        };
        var sessionDuplicate = new ContentRepository
        {
            Name = "Session",
            BaseUrl = "https://same.example/",
            Priority = 5
        };
        var disabled = new ContentRepository
        {
            Name = "Disabled",
            BaseUrl = "https://disabled.example",
            IsEnabled = false
        };
        var scope = Guid.NewGuid();

        var context = ContentSourceContext.Session(scope, [disabled, sessionDuplicate], [persistent, duplicate]);

        Assert.Equal(2, context.Candidates.Count);
        Assert.Equal(sessionDuplicate.Id, context.Candidates[0].Repository.Id);
        Assert.True(context.Candidates[0].IsSession);
        Assert.Equal(persistent.Id, context.Candidates[1].Repository.Id);
        Assert.False(context.Candidates[1].IsSession);
        Assert.Throws<ArgumentException>(() => ContentSourceContext.Session(Guid.Empty, [], []));
    }
}
