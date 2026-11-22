namespace Game.Blocks.Collision;

/// <summary>仅含位置和索引的不可变几何数据，无需材质或渲染资源。</summary>
public sealed class CollisionPrimitiveGeometry(IEnumerable<Vector3> positions)
{
    public IReadOnlyList<Vector3> Positions { get; } = Array.AsReadOnly(positions.ToArray());
}
