using Engine.Core;

namespace Engine.Serialization;

[HumanReadableConverter(typeof(Vector2))]
internal class Vector2HumanReadableConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        var vector = (Vector2)value;
        return HumanReadableConverter.ValuesListToString(',', vector.X, vector.Y);
    }

    public object ConvertFromString(Type type, string data)
    {
        var array = HumanReadableConverter.ValuesListFromString<float>(',', data);
        if (array.Length == 2)
        {
            return new Vector2(array[0], array[1]);
        }

        throw new Exception();
    }
}
