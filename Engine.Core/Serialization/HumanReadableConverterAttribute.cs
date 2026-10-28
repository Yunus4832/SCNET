namespace Engine.Core.Serialization;

[AttributeUsage(AttributeTargets.Class)]
public class HumanReadableConverterAttribute(Type type) : Attribute
{
    public Type Type = type;
}
