using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Engine.Core.Mathematics;
using Engine.Core.Runtime;

using EntitySystem.Core;

using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

[Collection(ConfigFileCollection.Name)]
public sealed class BlockCollisionGeometryBaselineTest
{
    [Fact]
    public void AllRuleCollisionFieldsMatchTheMeasuredGeometryBeforePresentationSeparation()
    {
        var baselinePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BlockCollisionGeometryBaseline.json");
        var expected = JsonSerializer.Deserialize<SortedDictionary<string, SortedDictionary<string, string>>>(
            File.ReadAllText(baselinePath)
        )!;

        {
            Dispatcher.Initialize();
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start();
            runtime.InitializeContentData();
            var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
            var catalog = runtime.CreateWorldCatalog(world, BlockIndexMapping.Empty);
            using var project = new Project();
            world.BindProject(project);
            try
            {
                var hashes = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
                foreach (var entry in catalog.Registry.ById.Values)
                {
                    var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
                    for (var type = entry.Block.GetType(); type is not null; type = type.BaseType)
                    {
                        foreach (var field in type.GetFields(
                                     BindingFlags.Public |
                                     BindingFlags.NonPublic |
                                     BindingFlags.Instance |
                                     BindingFlags.DeclaredOnly
                                 ))
                        {
                            var element = field.FieldType;
                            while (element.IsArray) { element = element.GetElementType()!; }

                            if (element != typeof(BoundingBox)) { continue; }

                            using var bytes = new MemoryStream();
                            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                            {
                                Write(writer, field.GetValue(entry.Block));
                            }

                            fields.Add(
                                type.Name + "." + field.Name,
                                Convert.ToHexString(SHA256.HashData(bytes.ToArray()))
                            );
                        }
                    }

                    if (fields.Count > 0) { hashes.Add(entry.Id.ToString(), fields); }
                }

                Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), hashes.Keys.Order(StringComparer.Ordinal));
                foreach (var (id, fields) in expected)
                {
                    Assert.Equal(
                        fields.Keys.Order(StringComparer.Ordinal),
                        hashes[id].Keys.Order(StringComparer.Ordinal)
                    );
                    foreach (var (field, hash) in fields)
                    {
                        Assert.True(hash == hashes[id][field], $"Collision geometry changed for {id}: {field}.");
                    }
                }
            }
            finally
            {
                world.Close();
            }
        }
    }

    private static void Write(BinaryWriter writer, object? value)
    {
        if (value is null)
        {
            writer.Write(-1);
            return;
        }

        if (value is Array array)
        {
            writer.Write(array.Length);
            foreach (var item in array) { Write(writer, item); }

            return;
        }

        var box = (BoundingBox)value;
        writer.Write(box.Min.X);
        writer.Write(box.Min.Y);
        writer.Write(box.Min.Z);
        writer.Write(box.Max.X);
        writer.Write(box.Max.Y);
        writer.Write(box.Max.Z);
    }
}
