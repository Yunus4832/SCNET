using Engine.Serialization;

namespace EntitySystem.TemplatesDatabase;

[HumanReadableConverter(typeof(ProceduralValue))]
public class ProceduralValueStringConverter : IHumanReadableConverter
{
    public string ConvertToString(object value)
    {
        return ((ProceduralValue)value).Procedure;
    }

    public object ConvertFromString(Type type, string data)
    {
        var proceduralValue = default(ProceduralValue);
        proceduralValue.Procedure = data;
        return proceduralValue;
    }
}
