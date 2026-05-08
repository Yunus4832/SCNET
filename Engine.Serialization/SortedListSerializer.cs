namespace Engine.Serialization;

internal class SortedListSerializer<TK, TV> : ISerializer<SortedList<TK, TV?>> where TK : notnull
{
    public void Serialize(InputArchive archive, ref SortedList<TK, TV?> value)
    {
        value = new SortedList<TK, TV?>();
        archive.SerializeDictionary(null, value);
    }

    public void Serialize(OutputArchive archive, SortedList<TK, TV?> value)
    {
        archive.SerializeDictionary(null, value);
    }
}
