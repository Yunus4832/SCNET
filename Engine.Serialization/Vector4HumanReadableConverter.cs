using Engine.Core;

namespace Engine.Serialization;

[HumanReadableConverter(typeof(Vector4))]
internal class Vector4HumanReadableConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        var vector = (Vector4)value;
        return HumanReadableConverter.ValuesListToString(',', vector.X, vector.Y, vector.Z, vector.W);
    }

    public object ConvertFromString(Type type, string data)
    {
        var array = HumanReadableConverter.ValuesListFromString<float>(',', data);
        if (array.Length == 4)
        {
            return new Vector4(array[0], array[1], array[2], array[3]);
        }

        throw new Exception();
    }
}
