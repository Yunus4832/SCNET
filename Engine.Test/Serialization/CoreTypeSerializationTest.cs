using Engine.Core.Mathematics;
using Engine.Serialization;

namespace Engine.Test.Serialization;

public sealed class CoreTypeSerializationTest
{
    [Theory]
    [InlineData("Vector3", typeof(Vector3))]
    [InlineData("Point3", typeof(Point3))]
    [InlineData("Color", typeof(Color))]
    [InlineData("Matrix", typeof(Matrix))]
    public void TypeShortNamesResolveAfterCoreNamespaceReorganization(string name, Type type)
    {
        Assert.Equal(type, TypeCache.FindType(name, false, true));
        Assert.Equal(name, TypeCache.GetShortTypeName(type.FullName!));
    }

    [Fact]
    public void HumanReadableVectorRoundTripsThroughDiscoveredConverter()
    {
        var expected = new Vector3(1.25f, -2.5f, 3.75f);
        var encoded = HumanReadableConverter.ConvertToString(expected);
        Assert.True(HumanReadableConverter.TryConvertFromString<Vector3>(encoded, out var actual));
        Assert.Equal(expected, actual);
    }
}
