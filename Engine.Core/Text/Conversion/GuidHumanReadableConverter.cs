namespace Engine.Core.Text.Conversion;

[HumanReadableConverter(typeof(Guid))]
internal class GuidHumanReadableConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        return ((Guid)value).ToString("D");
    }

    public object ConvertFromString(Type type, string data)
    {
        return Guid.Parse(data);
    }
}
