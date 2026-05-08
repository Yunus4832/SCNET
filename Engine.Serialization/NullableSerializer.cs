namespace Engine.Serialization;

internal class NullableSerializer<T> : ISerializer<T?> where T : struct
{
    public void Serialize(InputArchive archive, ref T? value)
    {
        var value2 = false;
        archive.Serialize("HasValue", ref value2);
        if (value2)
        {
            var value3 = default(T);
            archive.Serialize("Value", ref value3);
            value = value3;
        }
    }

    public void Serialize(OutputArchive archive, T? value)
    {
        if (value.HasValue)
        {
            archive.Serialize("HasValue", true);
            archive.Serialize("Value", value.Value);
        }
        else
        {
            archive.Serialize("HasValue", false);
        }
    }
}
