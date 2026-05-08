namespace Engine.Serialization;

internal class SortedDictionarySerializer<TK, TV> : ISerializer<SortedDictionary<TK, TV?>> where TK : notnull
{
    public void Serialize(InputArchive archive, ref SortedDictionary<TK, TV?> value)
    {
        value = new SortedDictionary<TK, TV?>();
        archive.SerializeDictionary(null, value);
    }

    public void Serialize(OutputArchive archive, SortedDictionary<TK, TV?> value)
    {
        archive.SerializeDictionary(null, value);
    }
}
