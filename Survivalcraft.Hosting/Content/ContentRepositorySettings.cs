namespace Game.Content;

public sealed record ContentRepositorySettings
{
    public Guid? GameInformationSourceId { get; init; }

    public IReadOnlyList<ContentRepository> Repositories { get; init; } = [];
}
