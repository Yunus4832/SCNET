namespace Engine.Core.Serialization;

public interface IHumanReadableConverter
{
    string ConvertToString(object value);

    object ConvertFromString(Type type, string data);
}
