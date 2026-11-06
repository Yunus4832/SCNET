using System.Collections.ObjectModel;

namespace Game.Blocks.Collision;

/// <summary>Shared immutable asset geometry. World-local builders are never stored here.</summary>
public sealed class CollisionGeometryCatalog(IEnumerable<CollisionModelGeometry> models)
{
    private readonly IReadOnlyDictionary<ResourceId, CollisionModelGeometry> _models =
        new ReadOnlyDictionary<ResourceId, CollisionModelGeometry>(models.ToDictionary(model => model.Id));

    public CollisionModelGeometry Get(ResourceId id) => _models[id];
}
