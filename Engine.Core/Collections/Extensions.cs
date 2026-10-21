namespace Engine.Core.Collections;

public static class Extensions
{
    public static DynamicArray<T> ToDynamicArray<T>(this IEnumerable<T> source)
    {
        return new DynamicArray<T>(source);
    }
}
