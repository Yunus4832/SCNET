namespace Game;

public interface IAStarStorage<in T> where T: unmanaged
{
    void Clear();

    object? Get(T p);

    void Set(T p, object? data);
}
