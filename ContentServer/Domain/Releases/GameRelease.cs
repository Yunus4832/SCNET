using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Releases;

public enum GameReleaseStatus { Draft, Published, Withdrawn }

public partial record GameReleaseId : IGuidStronglyTypedId;

public sealed class GameRelease : Entity<GameReleaseId>, IAggregateRoot
{
    private readonly List<GameReleaseArtifact> _artifacts = [];

    private GameRelease() { }

    public Deleted Deleted { get; private set; } = new(false);
    public string Version { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public IReadOnlyCollection<GameReleaseArtifact> Artifacts => _artifacts;
    public GameReleaseStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public static GameRelease Create(string version, string description,
        IEnumerable<GameReleaseArtifactData> artifacts, DateTimeOffset now)
    {
        Validate(version, description, artifacts, out var values);
        var release = new GameRelease
        {
            Version = version.Trim(),
            Description = description.Trim(),
            Status = GameReleaseStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        release._artifacts.AddRange(values.Select(item => GameReleaseArtifact.Create(release.Id, item)));
        return release;
    }

    public void Edit(string version, string description, IEnumerable<GameReleaseArtifactData> artifacts,
        DateTimeOffset now)
    {
        if (Status != GameReleaseStatus.Draft)
            throw new InvalidOperationException("Only draft releases can be edited.");
        Validate(version, description, artifacts, out var values);
        Version = version.Trim();
        Description = description.Trim();
        foreach (var artifact in _artifacts)
        {
            artifact.Delete();
        }

        _artifacts.AddRange(values.Select(item => GameReleaseArtifact.Create(Id, item)));
        UpdatedAt = now;
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != GameReleaseStatus.Draft)
            throw new InvalidOperationException("Only draft releases can be published.");
        Status = GameReleaseStatus.Published;
        PublishedAt = now;
        UpdatedAt = now;
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (Status != GameReleaseStatus.Published)
            throw new InvalidOperationException("Only published releases can be withdrawn.");
        Status = GameReleaseStatus.Withdrawn;
        UpdatedAt = now;
    }

    private static void Validate(string version, string description, IEnumerable<GameReleaseArtifactData> artifacts,
        out GameReleaseArtifactData[] values)
    {
        values = artifacts.ToArray();
        if (!System.Version.TryParse(version, out var parsed) || parsed.Major < 0 || description.Length > 10000 ||
            values.Length == 0 || values.Select(item => item.Platform).Distinct().Count() != values.Length)
            throw new ArgumentException("Invalid game release.");
        foreach (var artifact in values) GameReleaseArtifact.Validate(artifact);
    }
}

public sealed record GameReleaseArtifactData(string Platform, string DownloadUrl, string Sha256);
