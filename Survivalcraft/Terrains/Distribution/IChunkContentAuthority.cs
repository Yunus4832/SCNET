namespace Game.Terrains.Distribution;

public interface IChunkContentAuthority
{
    bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor);

    bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot snapshot);
}
