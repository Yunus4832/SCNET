namespace Engine.Core.Text.Conversion;

public interface IHumanReadableConverter
{
    string ConvertToString(object value);

    object ConvertFromString(Type type, string data);
}
