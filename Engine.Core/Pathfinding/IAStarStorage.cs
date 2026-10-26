namespace Engine.Core.Pathfinding;

public interface IAStarStorage<in T> where T : unmanaged
{
    void Clear();

    object? Get(T p);

    void Set(T p, object? data);
}
