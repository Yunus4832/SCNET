namespace Engine.Core.Text.Conversion;

[AttributeUsage(AttributeTargets.Class)]
public class HumanReadableConverterAttribute(Type type) : Attribute
{
    public Type Type = type;
}
