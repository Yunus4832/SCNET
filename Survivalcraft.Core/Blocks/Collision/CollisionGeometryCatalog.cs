using System.Collections.ObjectModel;

namespace Game.Blocks.Collision;

/// <summary>共享的不可变资源几何数据，世界专属的构建器由各世界自行保存。</summary>
public sealed class CollisionGeometryCatalog(IEnumerable<CollisionModelGeometry> models)
{
    private readonly IReadOnlyDictionary<ResourceId, CollisionModelGeometry> _models =
        new ReadOnlyDictionary<ResourceId, CollisionModelGeometry>(models.ToDictionary(model => model.Id));

    public CollisionModelGeometry Get(ResourceId id) => _models[id];
}
