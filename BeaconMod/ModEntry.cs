using System.Xml.Linq;

using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.Modding.Data;

namespace BeaconMod;

public sealed class ModEntry : IMod
{
    public const string ModId = "scnet.beacon";

    public void Configure(IModContext context)
    {
        foreach (var culture in new[] { "zh-CN", "en-US", "pt-PT", "ru-RU" })
        {
            using var stream = OpenResource($"Lang.{culture}.json");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var path = $"lang/{culture}.json";
            context.Extensions.RegisterContent(new ResourceId(context.Manifest.ModId, path), path, bytes.ToArray());
        }
        context.Extensions.RegisterBlock<BeaconBlock>(BeaconBlock.Id);
        context.Extensions.RegisterBlockData(new ResourceId(context.Manifest.ModId, "beacon"), () =>
        {
            using var reader = new StreamReader(OpenResource("BlocksData.txt"));
            return reader.ReadToEnd();
        });
        context.Extensions.RegisterXmlData(XmlDataExtensions.DatabaseRegistryName,
            new ResourceId(context.Manifest.ModId, "beacon"), XmlContributionMode.Patch, () =>
            {
                using var stream = OpenResource("BeaconDatabase.xml");
                return XElement.Load(stream);
            });
    }

    public void Start(IModContext context)
    {
    }

    public void Stop()
    {
    }

    internal static Stream OpenResource(string name) => typeof(ModEntry).Assembly.GetManifestResourceStream(
        $"BeaconMod.Resources.{name}") ?? throw new InvalidOperationException($"Missing beacon resource: {name}");
}
