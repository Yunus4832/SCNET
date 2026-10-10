using Game.Content;

namespace Game;

public sealed record ContentRepositorySettings
{
    public Guid? GameInformationSourceId { get; init; }
    public IReadOnlyList<ContentRepository> Repositories { get; init; } = [];
}
