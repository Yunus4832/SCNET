namespace Engine.Serialization;

internal class ArraySerializer<T> : ISerializer<Array>
{
    public void Serialize(InputArchive archive, ref Array value)
    {
        var list = new List<T?>();
        archive.SerializeCollection(null, list);
        value = list.ToArray();
    }

    public void Serialize(OutputArchive archive, Array value)
    {
        archive.SerializeCollection(null, "e", (T[])value);
    }
}
