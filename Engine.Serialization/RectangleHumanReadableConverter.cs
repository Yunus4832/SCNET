using Engine.Core;

namespace Engine.Serialization;

[HumanReadableConverter(typeof(Rectangle))]
internal class RectangleHumanReadableConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        var rectangle = (Rectangle)value;
        return HumanReadableConverter.ValuesListToString(',', rectangle.Left, rectangle.Top, rectangle.Width,
            rectangle.Height);
    }

    public object ConvertFromString(Type type, string data)
    {
        var array = HumanReadableConverter.ValuesListFromString<int>(',', data);
        if (array.Length == 4)
        {
            return new Rectangle(array[0], array[1], array[2], array[3]);
        }

        throw new Exception();
    }
}
