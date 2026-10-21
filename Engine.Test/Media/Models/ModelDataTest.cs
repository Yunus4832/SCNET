using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

using Engine.Core.Mathematics;
using Engine.Graphics;
using Engine.Media.Models;

namespace Engine.Test.Media.Models;

public class ModelDataTest
{
    [Fact]
    public void GameModelLoadsWithoutGraphicsInitialization()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestAssets", "StoneClub.glb"));
        var model = ModelData.Load(stream);
        Assert.NotEmpty(model.Nodes);
        Assert.NotEmpty(model.Meshes);
        Assert.NotEmpty(model.Accessors);
        Assert.All(model.Meshes.SelectMany(m => m.MeshParts), p => Assert.Contains("POSITION", p.Attributes.Keys));
    }

    [Fact]
    public void BlenderSharedMeshAndAuthoredHierarchyArePreserved()
    {
        var model = Blender("StaticTextured");
        Assert.Equal(3, model.Nodes.Count);
        Assert.Single(model.Meshes);
        Assert.Equal(2, model.Nodes.Count(n => n.MeshIndex == 0));
        var parent = Assert.Single(model.Nodes, n => n.Name == "ParentPivot");
        Assert.Single(parent.Children);
        Assert.Equal("VisiblePart", model.Nodes[parent.Children[0]].Name);
        Assert.Single(model.Images);
        Assert.Equal("image/png", model.Images[0].MimeType);
        Assert.NotEmpty(model.Images[0].EncodedData);
        Assert.NotNull(model.Materials[0].BaseColorTexture);
    }

    [Fact]
    public void BlenderMaterialsRemainAttachedToTheirPrimitives()
    {
        var model = Blender("MultiMaterial");
        Assert.Equal(2, model.Materials.Count);
        var parts = Assert.Single(model.Meshes).MeshParts;
        Assert.Equal(2, parts.Count);
        Assert.Equal(2, parts.Select(p => p.MaterialIndex).Distinct().Count());
        Assert.Contains(model.Materials, m => m.BaseColorFactor.X > m.BaseColorFactor.Z);
        Assert.Contains(model.Materials, m => m.BaseColorFactor.Z > m.BaseColorFactor.X);
    }

    [Fact]
    public void BlenderSkinAndAnimationRetainNodeAndAccessorReferences()
    {
        var model = Blender("SkinnedAnimated");
        var skin = Assert.Single(model.Skins);
        Assert.Equal(2, skin.JointNodes.Length);
        Assert.Equal(skin.JointNodes.Length, skin.InverseBindMatrices.Length);
        Assert.All(skin.JointNodes, i => Assert.InRange(i, 0, model.Nodes.Count - 1));
        Assert.Contains(model.Nodes, n => n.SkinIndex == 0);
        var clip = Assert.Single(model.Animations);
        Assert.NotEmpty(clip.Channels);
        Assert.All(
            clip.Samplers,
            s =>
            {
                Assert.True(model.Accessors[s.InputAccessorIndex].Count > 0);
                Assert.True(model.Accessors[s.OutputAccessorIndex].Count > 0);
            }
        );
        var primitive = Assert.Single(Assert.Single(model.Meshes).MeshParts);
        Assert.Contains("JOINTS_0", primitive.Attributes.Keys);
        Assert.Contains("WEIGHTS_0", primitive.Attributes.Keys);
        Assert.Throws<NotSupportedException>(() => Model.Load(model));
    }

    [Fact]
    public void BlenderDefaultShapeWeightsAffectPreparedGeometry()
    {
        var model = Blender("ShapeKeys");
        var mesh = Assert.Single(model.Meshes);
        var part = Assert.Single(mesh.MeshParts);
        Assert.Equal(0.5f, Assert.Single(mesh.MorphWeights));
        Assert.Single(part.MorphTargets);
        var basis = ModelGeometryData.Create(model, part, []);
        var morphed = ModelGeometryData.Create(model, part, mesh.MorphWeights);
        Assert.True(morphed.BoundingBox.Max.Y > basis.BoundingBox.Max.Y);
        Assert.Equal(basis.Indices, morphed.Indices);
    }

    [Fact]
    public void NonindexedGeometryWithoutNormalsOrUvGetsFlatNormalsAtRendererBoundary()
    {
        var model = Triangle();
        Assert.DoesNotContain("NORMAL", model.Meshes[0].MeshParts[0].Attributes.Keys);
        var geometry = ModelGeometryData.Create(model, model.Meshes[0].MeshParts[0], []);
        Assert.Equal(new uint[] { 0, 2, 1 }, geometry.Indices);
        Assert.Equal(3 * 32, geometry.Vertices.Length);
        Assert.Equal(1, BinaryPrimitives.ReadSingleLittleEndian(geometry.Vertices.AsSpan(20)));
        Assert.Equal(0, BinaryPrimitives.ReadSingleLittleEndian(geometry.Vertices.AsSpan(24)));
    }

    [Fact]
    public void SparseAccessorWithNoBaseBufferIsExpanded()
    {
        var bytes = new byte[16];
        bytes[0] = 2;
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8), 3);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 4);
        var json = JsonNode.Parse(
            """
            {"asset":{"version":"2.0"},"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":1},{"buffer":0,"byteOffset":4,"byteLength":12}],
             "accessors":[{"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[2,3,4],"sparse":{"count":1,"indices":{"bufferView":0,"componentType":5121},"values":{"bufferView":1}}}]}
            """
        )!.AsObject();
        var model = Load(json, bytes);
        Assert.Equal(Vector3.Zero, model.Accessors[0].GetVector3(0));
        Assert.Equal(new Vector3(2, 3, 4), model.Accessors[0].GetVector3(2));
        Assert.Empty(model.Scenes);
    }

    [Fact]
    public void MatrixColumnPaddingAndUnsignedPrecisionArePreserved()
    {
        var bytes = new byte[] { 1, 2, 3, 0, 4, 5, 6, 0, 7, 8, 9, 0, 255, 255, 255, 255 };
        var json = JsonNode.Parse(
            """
            {"asset":{"version":"2.0"},"bufferViews":[{"buffer":0,"byteLength":12},{"buffer":0,"byteOffset":12,"byteLength":4}],
             "accessors":[{"bufferView":0,"componentType":5121,"count":1,"type":"MAT3"},{"bufferView":1,"componentType":5125,"count":1,"type":"SCALAR"}]}
            """
        )!.AsObject();
        var model = Load(json, bytes);
        Assert.Equal(Enumerable.Range(1, 9).Select(i => (byte)i), model.Accessors[0].Data);
        Assert.Equal(uint.MaxValue, model.Accessors[1].GetComponent(0, 0));
    }

    [Fact]
    public void ExternalBuffersResolveThroughCallerAndLoaderDisposesResource()
    {
        var bytes = Positions();
        var json = TriangleJson();
        json["buffers"] = new JsonArray(new JsonObject { ["uri"] = "mesh.bin", ["byteLength"] = bytes.Length });
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()));
        var resource = new MemoryStream(bytes);
        var model = ModelData.Load(
            input,
            uri =>
            {
                Assert.Equal("mesh.bin", uri);
                return resource;
            }
        );
        Assert.Single(model.Meshes);
        Assert.False(resource.CanRead);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void MultipleRootsAndDuplicateNamesDoNotCreateSyntheticAssetNodes()
    {
        var json = TriangleJson();
        json["nodes"] = new JsonArray(
            new JsonObject { ["name"] = "same", ["mesh"] = 0 },
            new JsonObject { ["name"] = "same", ["mesh"] = 0 }
        );
        json["scenes"]![0]!["nodes"] = new JsonArray(0, 1);
        var model = Load(json, Positions());
        Assert.Equal(2, model.Nodes.Count);
        Assert.Single(model.Meshes);
        Assert.Equal(new[] { 0, 1 }, model.Scenes[0].RootNodes);
        Assert.Equal(-1, model.DefaultSceneIndex);
    }

    [Fact]
    public void MissingResourceAndRequiredUnsupportedExtensionFailExplicitly()
    {
        var json = TriangleJson();
        json["buffers"] = new JsonArray(new JsonObject { ["uri"] = "missing.bin", ["byteLength"] = 36 });
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Contains(
            "resource",
            Assert.ThrowsAny<Exception>(() => ModelData.Load(input)).ToString(),
            StringComparison.OrdinalIgnoreCase
        );
        json["extensionsRequired"] = new JsonArray("VENDOR_missing_feature");
        json["extensionsUsed"] = new JsonArray("VENDOR_missing_feature");
        Assert.ThrowsAny<Exception>(() => Load(json, Positions()));
    }

    [Fact]
    public void InterleavedNormalizedAttributesRetainExactEncodingAndMetadata()
    {
        var bytes = new byte[60];
        for (var i = 0; i < 3; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 20), i);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 20 + 12), ushort.MaxValue);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 20 + 14), 32768);
        }

        var json = TriangleJson();
        json["bufferViews"] =
            new JsonArray(new JsonObject { ["buffer"] = 0, ["byteLength"] = 60, ["byteStride"] = 20 });
        json["accessors"]![0]!["max"] = new JsonArray(2, 0, 0);
        json["accessors"]!.AsArray().Add(
            JsonNode.Parse(
                """
                {"bufferView":0,"byteOffset":12,"componentType":5123,"normalized":true,"count":3,"type":"VEC4","extras":{"purpose":"test"}}
                """
            )
        );
        json["meshes"]![0]!["primitives"]![0]!["attributes"]!["COLOR_0"] = 1;
        json["extensionsUsed"] = new JsonArray("VENDOR_metadata");
        json["extensions"] = JsonNode.Parse("""{"VENDOR_metadata":{"value":42}}""");
        var model = Load(json, bytes);
        Assert.Equal(new Vector3(2, 0, 0), model.Accessors[0].GetVector3(2));
        var color = model.Accessors[1];
        Assert.True(color.Normalized);
        Assert.Equal(ModelComponentType.UnsignedShort, color.ComponentType);
        Assert.Equal(24, color.Data.Length);
        Assert.Equal(1, color.GetComponent(0, 0));
        Assert.Equal(32768 / 65535.0, color.GetComponent(1, 1));
        Assert.Equal("test", color.Extras!["purpose"]!.GetValue<string>());
        Assert.Equal(42, model.Extensions["VENDOR_metadata"]!["value"]!.GetValue<int>());
    }

    [Fact]
    public void LargeGeometryRetains32BitIndicesThroughCpuPreparation()
    {
        const int count = 65537;
        var bytes = new byte[count * 24 + 12];
        for (var i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(count * 12 + i * 12 + 8), 1);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(count * 24), 65536);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(count * 24 + 4), 1);
        var json = TriangleJson();
        json["bufferViews"] = JsonNode.Parse(
            $$"""
              [{"buffer":0,"byteLength":{{count * 12}}},{"buffer":0,"byteOffset":{{count * 12}},"byteLength":{{count * 12}}},{"buffer":0,"byteOffset":{{count * 24}},"byteLength":12}]
              """
        );
        json["accessors"] = JsonNode.Parse(
            $$"""
              [{"bufferView":0,"componentType":5126,"count":{{count}},"type":"VEC3","min":[0,0,0],"max":[0,0,0]},
               {"bufferView":1,"componentType":5126,"count":{{count}},"type":"VEC3"},
               {"bufferView":2,"componentType":5125,"count":3,"type":"SCALAR"}]
              """
        );
        json["meshes"]![0]!["primitives"]![0]!["indices"] = 2;
        json["meshes"]![0]!["primitives"]![0]!["attributes"]!["NORMAL"] = 1;
        var model = Load(json, bytes);
        var geometry = ModelGeometryData.Create(model, model.Meshes[0].MeshParts[0], []);
        Assert.Equal(count * 32, geometry.Vertices.Length);
        Assert.Equal(new uint[] { 65536, 0, 1 }, geometry.Indices);
    }

    [Fact]
    public void AuthoredTrsIncludingSignedScaleIsNotLostToMatrixDecomposition()
    {
        var json = TriangleJson();
        json["nodes"]![0]!["scale"] = new JsonArray(-1, -1, 1);
        json["nodes"]![0]!["translation"] = new JsonArray(2, 3, 4);
        json["nodes"]![0]!["rotation"] = new JsonArray(0, 0, 0, 1);
        var model = Load(json, Positions());
        var node = Assert.Single(model.Nodes);
        Assert.Null(node.MatrixTransform);
        Assert.Equal(new Vector3(-1, -1, 1), node.Scale);
        Assert.Equal(Quaternion.Identity, node.Rotation);
        Assert.Equal(new Vector3(2, 3, 4), node.Translation);
    }

    private static ModelData Blender(string name)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestAssets", "Models", name + ".glb"));
        return ModelData.Load(stream);
    }

    private static ModelData Triangle() => Load(TriangleJson(), Positions());

    private static JsonObject TriangleJson()
    {
        return JsonNode.Parse(
            """
            {"asset":{"version":"2.0"},"bufferViews":[{"buffer":0,"byteLength":36}],
             "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]}],
             "meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],"nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}]}
            """
        )!.AsObject();
    }

    private static byte[] Positions()
    {
        var bytes = new byte[36];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(28), 1);
        return bytes;
    }

    private static ModelData Load(JsonObject json, byte[] bytes)
    {
        json["buffers"] = new JsonArray(
            new JsonObject
            {
                ["byteLength"] = bytes.Length,
                ["uri"] = "data:application/octet-stream;base64," + Convert.ToBase64String(bytes)
            }
        );
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()));
        return ModelData.Load(stream);
    }
}
