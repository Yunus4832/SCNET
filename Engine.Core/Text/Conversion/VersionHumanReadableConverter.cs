namespace Engine.Core.Text.Conversion;

[HumanReadableConverter(typeof(Version))]
internal class VersionHumanReadableConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        return ((Version)value).ToString();
    }

    public object ConvertFromString(Type type, string data)
    {
        return Version.Parse(data);
    }
}
