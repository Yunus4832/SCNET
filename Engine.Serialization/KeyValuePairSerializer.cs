namespace Engine.Serialization;

internal class KeyValuePairSerializer<TK, TV> : ISerializer<KeyValuePair<TK, TV?>> where TK: notnull
{
    public void Serialize(InputArchive archive, ref KeyValuePair<TK, TV?> value)
    {
        var value2 = default(TK);
        var value3 = default(TV);
        archive.Serialize("K", ref value2);
        archive.Serialize("V", ref value3);
        if (value2 is null)
        {
            throw new InvalidOperationException("Dictionary key is null");
        }

        value = new KeyValuePair<TK, TV?>(value2, value3);
    }

    public void Serialize(OutputArchive archive, KeyValuePair<TK, TV?> value)
    {
        archive.Serialize("K", value.Key);
        archive.Serialize("V", value.Value);
    }
}
