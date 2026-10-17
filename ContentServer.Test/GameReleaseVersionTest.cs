using ContentServer.Domain.Releases;

namespace ContentServer.Test;

public sealed class GameReleaseVersionTest
{
    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.1.0-alpha.1")]
    public void ReleasesAcceptSemanticVersions(string version)
    {
        var release = GameRelease.Create(version, "Update",
            [new GameReleaseArtifactData("linux-x64", "https://example.test/game.zip", new string('a', 64))], DateTimeOffset.UnixEpoch);
        Assert.Equal(version, release.Version);
    }

    [Theory]
    [InlineData("0.0.0.1")]
    [InlineData("01.1.0")]
    [InlineData("0.1")]
    public void ReleasesRejectNonSemanticVersions(string version)
    {
        Assert.Throws<ArgumentException>(() => GameRelease.Create(version, "Update",
            [new GameReleaseArtifactData("linux-x64", "https://example.test/game.zip", new string('a', 64))], DateTimeOffset.UnixEpoch));
    }
}
