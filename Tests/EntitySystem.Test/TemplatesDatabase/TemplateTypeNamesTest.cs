using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Test.TemplatesDatabase;

public class TemplateTypeNamesTest
{
    [Theory]
    [InlineData("int", typeof(int))]
    [InlineData("Vector3", typeof(Vector3))]
    [InlineData("Point3", typeof(Point3))]
    [InlineData("Color", typeof(Color))]
    [InlineData("Matrix", typeof(Matrix))]
    public void FormatAliasesResolveAndWriteWithoutChangingExistingXmlNames(string name, Type type)
    {
        Assert.Equal(type, TemplateTypeNames.Resolve(name));
        Assert.Equal(name, TemplateTypeNames.GetShortTypeName(type.FullName!));
    }

    [Fact]
    public void CustomTypesUseTheirFullRuntimeName()
    {
        Assert.Equal(typeof(ProceduralValue), TemplateTypeNames.Resolve(typeof(ProceduralValue).FullName!));
        Assert.Equal(
            typeof(ProceduralValue).FullName,
            TemplateTypeNames.GetShortTypeName(typeof(ProceduralValue).FullName!)
        );
        Assert.Null(TemplateTypeNames.Resolve("Missing.Template.Type", throwIfNotFound: false));
    }
}
