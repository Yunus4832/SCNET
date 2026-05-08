using Engine.Core;

namespace Engine.Serialization;

[HumanReadableConverter(typeof(BoundingCircle))]
internal class BoundingCircleHumanReadableConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        var boundingCircle = (BoundingCircle)value;
        return HumanReadableConverter.ValuesListToString(',', boundingCircle.Center.X, boundingCircle.Center.Y,
            boundingCircle.Radius);
    }

    public object ConvertFromString(Type type, string data)
    {
        var array = HumanReadableConverter.ValuesListFromString<float>(',', data);
        if (array.Length == 3)
        {
            return new BoundingCircle(new Vector2(array[0], array[1]), array[2]);
        }

        throw new Exception();
    }
}
