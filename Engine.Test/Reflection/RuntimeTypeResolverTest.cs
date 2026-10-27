using Engine.Core.Reflection;

namespace Engine.Test.Reflection;

public class RuntimeTypeResolverTest
{
    [Fact]
    public void RuntimeNamesResolveWithoutTemplateFormatAliases()
    {
        Assert.Equal(typeof(int), RuntimeTypeResolver.FindType(typeof(int).FullName!));
        Assert.Equal(
            typeof(RuntimeTypeResolverTest),
            RuntimeTypeResolver.FindType(typeof(RuntimeTypeResolverTest).FullName!)
        );
        Assert.Null(RuntimeTypeResolver.FindType("int", throwIfNotFound: false));
        Assert.Throws<InvalidOperationException>(() => RuntimeTypeResolver.FindType("Unknown.Runtime.Type"));
    }
}
