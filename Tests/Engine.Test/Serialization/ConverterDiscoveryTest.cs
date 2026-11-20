using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

using Engine.Core.Reflection;
using Engine.Core.Serialization;

namespace Engine.Test.Serialization;

public class ConverterDiscoveryTest
{
    public sealed record LateValue(string Text);

    public class ConverterBase : IHumanReadableConverter
    {
        public string ConvertToString(object value) => ((LateValue)value).Text;

        public object ConvertFromString(Type type, string data) => new LateValue(data);
    }

    [Fact]
    public void LateConverterIsDiscoveredAndCollectibleAssemblyIsNotRetained()
    {
        Assert.False(HumanReadableConverter.IsTypeSupported(typeof(LateValue)));
        var reference = CreateAndUseConverter();
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(reference.IsAlive);
        Assert.False(HumanReadableConverter.IsTypeSupported(typeof(LateValue)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndUseConverter()
    {
        var typeName = "LateTextConverter_" + Guid.NewGuid().ToString("N");
        Assert.Null(RuntimeTypeResolver.FindType(typeName, throwIfNotFound: false));
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(typeName),
            AssemblyBuilderAccess.RunAndCollect
        );
        var module = assembly.DefineDynamicModule(typeName);
        var builder = module.DefineType(typeName, TypeAttributes.Public, typeof(ConverterBase));
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        builder.SetCustomAttribute(
            new CustomAttributeBuilder(
                typeof(HumanReadableConverterAttribute).GetConstructor([typeof(Type)])!,
                [typeof(LateValue)]
            )
        );
        var type = builder.CreateType()!;
        Assert.Equal(type, RuntimeTypeResolver.FindType(typeName));
        Assert.True(HumanReadableConverter.IsTypeSupported(typeof(LateValue)));
        Assert.Equal(new LateValue("late"), HumanReadableConverter.ConvertFromString<LateValue>("late"));
        Assert.Equal("written", HumanReadableConverter.ConvertToString(new LateValue("written")));
        return new WeakReference(type.Assembly);
    }

    public sealed record ModuleValue(string Text);

    public class ModuleConverterBase : IHumanReadableConverter
    {
        public string ConvertToString(object value) => ((ModuleValue)value).Text;

        public object ConvertFromString(Type type, string data) => new ModuleValue(data);
    }

    [Fact]
    public void CachedConverterCatalogDoesNotPreventModuleContextUnload()
    {
        Assert.False(HumanReadableConverter.IsTypeSupported(typeof(ModuleValue)));
        var reference = LoadAndUnloadModule();
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(reference.IsAlive);
        Assert.False(HumanReadableConverter.IsTypeSupported(typeof(ModuleValue)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndUnloadModule()
    {
        var name = "ConverterModule_" + Guid.NewGuid().ToString("N");
        var builder = new PersistedAssemblyBuilder(new AssemblyName(name), typeof(object).Assembly);
        var module = builder.DefineDynamicModule(name);
        var type = module.DefineType(name, TypeAttributes.Public, typeof(ModuleConverterBase));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        type.SetCustomAttribute(
            new CustomAttributeBuilder(
                typeof(HumanReadableConverterAttribute).GetConstructor([typeof(Type)])!,
                [typeof(ModuleValue)]
            )
        );
        type.CreateType();
        using var stream = new MemoryStream();
        builder.Save(stream);
        stream.Position = 0;
        var context = new AssemblyLoadContext(name, isCollectible: true);
        var assembly = context.LoadFromStream(stream);
        Assert.False(assembly.IsDynamic);
        Assert.Equal(assembly.GetType(name), RuntimeTypeResolver.FindType(name));
        Assert.Equal(new ModuleValue("module"), HumanReadableConverter.ConvertFromString<ModuleValue>("module"));
        var reference = new WeakReference(context);
        context.Unload();
        return reference;
    }
}
