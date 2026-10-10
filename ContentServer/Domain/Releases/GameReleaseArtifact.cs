using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Releases;

public partial record GameReleaseArtifactId : IGuidStronglyTypedId;

public sealed class GameReleaseArtifact : Entity<GameReleaseArtifactId>
{
    private GameReleaseArtifact() { }

    public Deleted Deleted { get; private set; } = new(false);
    public GameReleaseId GameReleaseId { get; private set; } = default!;
    public GameRelease Owner { get; private set; } = default!;
    public string Platform { get; private set; } = string.Empty;
    public string DownloadUrl { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;

    internal static GameReleaseArtifact Create(GameReleaseId releaseId, GameReleaseArtifactData data)
    {
        Validate(data);
        return new GameReleaseArtifact
        {
            GameReleaseId = releaseId,
            Platform = data.Platform,
            DownloadUrl = data.DownloadUrl.Trim(),
            Sha256 = data.Sha256.ToLowerInvariant()
        };
    }

    internal static void Validate(GameReleaseArtifactData data)
    {
        if (data.Platform is not ("windows-x64" or "linux-x64" or "android-arm64" or "android-arm32") ||
            data.Sha256.Length != 64 || !data.Sha256.All(Uri.IsHexDigit) ||
            !Uri.TryCreate(data.DownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Invalid game release artifact.");
    }

    internal void Delete()
    {
        Deleted = new Deleted(true);
    }
}
