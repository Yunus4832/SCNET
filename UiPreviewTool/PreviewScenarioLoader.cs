using System.Runtime.Loader;
using System.Xml.Linq;

using Game.Widgets;

namespace Scnet.UiPreviewTool;

internal static class PreviewScenarioLoader
{
    public static IUiPreviewScenario Load(PreviewOptions options)
    {
        if (options.XmlPath is { } xml)
        {
            return new XmlScenario(xml);
        }
        var path = options.AssemblyPath!;
        var resolver = new AssemblyDependencyResolver(path);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var dependency = resolver.ResolveAssemblyToPath(name);
            dependency ??= Path.Combine(Path.GetDirectoryName(path)!, name.Name + ".dll");
            return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
        };
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        var type = assembly.GetType(options.FactoryType!, throwOnError: true)!;
        if (!typeof(IUiPreviewScenario).IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new ArgumentException("Factory must implement IUiPreviewScenario and have a public parameterless constructor.");
        }
        return (IUiPreviewScenario)Activator.CreateInstance(type)!;
    }

    private sealed class XmlScenario(string path) : IUiPreviewScenario
    {
        public Widget Create(UiPreviewContext context) => Widget.LoadWidget(null, XElement.Load(path), null);
    }
}
