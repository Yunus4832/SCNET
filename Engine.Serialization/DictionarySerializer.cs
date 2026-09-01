namespace Engine.Serialization;

internal class DictionarySerializer<TK, TV> : ISerializer<Dictionary<TK, TV?>> where TK : notnull
{
    public void Serialize(InputArchive archive, ref Dictionary<TK, TV?> value)
    {
        value = new Dictionary<TK, TV?>();
        archive.SerializeDictionary(null, value);
    }

    public void Serialize(OutputArchive archive, Dictionary<TK, TV?> value)
    {
        archive.SerializeDictionary(null, value);
    }
}
