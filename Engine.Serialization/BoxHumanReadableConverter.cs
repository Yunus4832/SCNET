using Engine.Core;

namespace Engine.Serialization;

[HumanReadableConverter(typeof(Box))]
internal class BoxHumanReadableConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        var box = (Box)value;
        return HumanReadableConverter.ValuesListToString(',', box.Left, box.Top, box.Near, box.Width, box.Height,
            box.Depth);
    }

    public object ConvertFromString(Type type, string data)
    {
        var array = HumanReadableConverter.ValuesListFromString<int>(',', data);
        if (array.Length == 6)
        {
            return new Box(array[0], array[1], array[2], array[3], array[4], array[5]);
        }

        throw new Exception();
    }
}
