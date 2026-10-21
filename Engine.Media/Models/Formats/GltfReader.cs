using System.Buffers.Binary;
using System.Text.Json.Nodes;

using Engine.Core.Mathematics;

using SharpGLTF.Schema2;

namespace Engine.Media.Models.Formats;

internal static class GltfReader
{
    public static ModelData Load(Stream stream, ModelFileFormat format, Func<string, Stream>? resolver)
    {
        var context = ReadContext.Create(uri =>
            {
                if (resolver == null)
                {
                    throw new InvalidDataException(
                        $"Model requires external resource '{uri}'; provide a resource resolver."
                    );
                }

                using var resource = resolver(Uri.UnescapeDataString(uri));
                using var bytes = new MemoryStream();
                resource.CopyTo(bytes);
                return bytes.ToArray();
            }
        );
        var source = format == ModelFileFormat.Glb
            ? context.ReadBinarySchema2(stream)
            : context.ReadTextSchema2(stream);
        var json = JsonNode.Parse(source.GetJsonPreview())!.AsObject();
        var model = new ModelData
        {
            DefaultSceneIndex = Int(json, "scene"),
            ExtensionsUsed = source.ExtensionsUsed.ToArray(),
            ExtensionsRequired = source.ExtensionsRequired.ToArray()
        };
        Metadata(model, json);
        for (var i = 0; i < source.LogicalAccessors.Count; i++)
        {
            model.Accessors.Add(ReadAccessor(source, source.LogicalAccessors[i], json["accessors"]![i]!.AsObject()));
        }

        foreach (var scene in source.LogicalScenes)
        {
            var data = new ModelSceneData();
            Metadata(data, json["scenes"]![scene.LogicalIndex]!.AsObject());
            data.RootNodes.AddRange(scene.VisualChildren.Select(n => n.LogicalIndex));
            model.Scenes.Add(data);
        }

        foreach (var node in source.LogicalNodes)
        {
            var item = json["nodes"]![node.LogicalIndex]!.AsObject();
            var data = new ModelNodeData
            {
                MatrixTransform = item["matrix"] == null ? null : MatrixFrom(node.LocalMatrix),
                Translation = Vector3From(item["translation"]),
                Scale = Vector3From(item["scale"], Vector3.One),
                Rotation = item["rotation"] is { } rotation
                    ? new Quaternion(
                        rotation[0]!.GetValue<float>(),
                        rotation[1]!.GetValue<float>(),
                        rotation[2]!.GetValue<float>(),
                        rotation[3]!.GetValue<float>()
                    )
                    : Quaternion.Identity,
                MeshIndex = node.Mesh?.LogicalIndex ?? -1,
                SkinIndex = node.Skin?.LogicalIndex ?? -1,
                CameraIndex = node.Camera?.LogicalIndex ?? -1,
                MorphWeights = item["weights"]?.AsArray().Select(v => v!.GetValue<float>()).ToArray()
            };
            Metadata(data, item);
            data.Children.AddRange(node.VisualChildren.Select(n => n.LogicalIndex));
            model.Nodes.Add(data);
        }

        foreach (var mesh in source.LogicalMeshes)
        {
            var item = json["meshes"]![mesh.LogicalIndex]!.AsObject();
            var data = new ModelMeshData { MorphWeights = mesh.MorphWeights.ToArray() };
            Metadata(data, item);
            foreach (var primitive in mesh.Primitives)
            {
                var part = new ModelMeshPartData
                {
                    Topology = (ModelPrimitiveTopology)primitive.DrawPrimitiveType,
                    IndicesAccessorIndex = primitive.IndexAccessor?.LogicalIndex ?? -1,
                    MaterialIndex = primitive.Material?.LogicalIndex ?? -1
                };
                Metadata(part, item["primitives"]![primitive.LogicalIndex]!.AsObject());
                foreach (var pair in primitive.VertexAccessors)
                {
                    part.Attributes.Add(pair.Key, pair.Value.LogicalIndex);
                }

                for (var i = 0; i < primitive.MorphTargetsCount; i++)
                {
                    part.MorphTargets.Add(
                        primitive.GetMorphTargetAccessors(i).ToDictionary(p => p.Key, p => p.Value.LogicalIndex)
                    );
                }

                if (part.Attributes.TryGetValue("POSITION", out var accessorIndex))
                {
                    var positions = model.Accessors[accessorIndex];
                    part.BoundingBox =
                        new BoundingBox(Enumerable.Range(0, positions.Count).Select(positions.GetVector3));
                }

                data.BoundingBox = data.MeshParts.Count == 0
                    ? part.BoundingBox
                    : BoundingBox.Union(data.BoundingBox, part.BoundingBox);
                data.MeshParts.Add(part);
            }

            model.Meshes.Add(data);
        }

        foreach (var skin in source.LogicalSkins)
        {
            var data = new ModelSkinData
            {
                SkeletonNodeIndex = skin.Skeleton?.LogicalIndex ?? -1,
                JointNodes = skin.Joints.Select(n => n.LogicalIndex).ToArray(),
                InverseBindMatrices = Enumerable.Range(0, skin.JointsCount)
                    .Select(i => MatrixFrom(skin.GetJoint(i).Item2)).ToArray()
            };
            Metadata(data, json["skins"]![skin.LogicalIndex]!.AsObject());
            model.Skins.Add(data);
        }

        ReadMaterials(model, json);
        foreach (var image in source.LogicalImages)
        {
            var data = new ModelImageData
                { EncodedData = image.Content.Content.ToArray(), MimeType = image.Content.MimeType };
            Metadata(data, json["images"]![image.LogicalIndex]!.AsObject());
            model.Images.Add(data);
        }

        foreach (var item in Objects(json, "textures"))
        {
            var data = new ModelTextureData { ImageIndex = Int(item, "source"), SamplerIndex = Int(item, "sampler") };
            Metadata(data, item);
            model.Textures.Add(data);
        }

        foreach (var item in Objects(json, "samplers"))
        {
            var data = new ModelSamplerData
            {
                MinFilter = item["minFilter"]?.GetValue<int>(), MagFilter = item["magFilter"]?.GetValue<int>(),
                WrapS = Int(item, "wrapS", 10497), WrapT = Int(item, "wrapT", 10497)
            };
            Metadata(data, item);
            model.Samplers.Add(data);
        }

        foreach (var item in Objects(json, "cameras"))
        {
            var type = item["type"]!.GetValue<string>();
            var data = new ModelCameraData { Type = type, Projection = item[type]!.DeepClone().AsObject() };
            Metadata(data, item);
            model.Cameras.Add(data);
        }

        ReadAnimations(model, json);
        return model;
    }

    private static ModelAccessorData ReadAccessor(ModelRoot root, Accessor source, JsonObject item)
    {
        var data = new ModelAccessorData
        {
            ComponentType = (ModelComponentType)source.Encoding, Dimensions = source.Dimensions.ToString(),
            Count = source.Count, Normalized = source.Normalized,
            Minimum = source.Bounds.Min.ToArray(), Maximum = source.Bounds.Max.ToArray()
        };
        Metadata(data, item);
        var componentSize = data.ComponentSize;
        var elementSize = checked(data.ComponentCount * componentSize);
        data.Data = new byte[checked(data.Count * elementSize)];
        var columns = data.Dimensions switch { "MAT2" => 2, "MAT3" => 3, "MAT4" => 4, _ => 1 };
        var rows = data.ComponentCount / columns;
        var columnStride = columns == 1 ? elementSize : (rows * componentSize + 3) / 4 * 4;
        var sourceSize = columns * columnStride;

        void CopyElement(ReadOnlySpan<byte> bytes, int offset, int target)
        {
            for (var column = 0; column < columns; column++)
            {
                bytes.Slice(checked(offset + column * columnStride), rows * componentSize)
                    .CopyTo(data.Data.AsSpan(checked(target * elementSize + column * rows * componentSize)));
            }
        }

        if (source.SourceBufferView is { } view)
        {
            var stride = view.ByteStride == 0 ? sourceSize : view.ByteStride;
            for (var i = 0; i < data.Count; i++)
            {
                CopyElement(view.Content.AsSpan(), checked(source.ByteOffset + i * stride), i);
            }
        }

        if (item["sparse"] is JsonObject sparse)
        {
            var indices = sparse["indices"]!.AsObject();
            var values = sparse["values"]!.AsObject();
            var indexBytes = root.LogicalBufferViews[Int(indices, "bufferView")].Content.AsSpan();
            var valueBytes = root.LogicalBufferViews[Int(values, "bufferView")].Content.AsSpan();
            var encoding = Int(indices, "componentType");
            var indexSize = encoding switch
            {
                5121 => 1, 5123 => 2, 5125 => 4, _ => throw new InvalidDataException("Invalid sparse index type.")
            };
            for (var i = 0; i < Int(sparse, "count"); i++)
            {
                var offset = checked(Int(indices, "byteOffset", 0) + i * indexSize);
                var target = encoding switch
                {
                    5121 => indexBytes[offset],
                    5123 => BinaryPrimitives.ReadUInt16LittleEndian(indexBytes[offset..]),
                    _ => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(indexBytes[offset..]))
                };
                CopyElement(valueBytes, checked(Int(values, "byteOffset", 0) + i * sourceSize), target);
            }
        }

        return data;
    }

    private static void ReadMaterials(ModelData model, JsonObject root)
    {
        foreach (var item in Objects(root, "materials"))
        {
            var pbr = item["pbrMetallicRoughness"]?.AsObject();
            var data = new ModelMaterialData
            {
                BaseColorFactor = Vector4From(pbr?["baseColorFactor"], Vector4.One),
                MetallicFactor = pbr?["metallicFactor"]?.GetValue<float>() ?? 1,
                RoughnessFactor = pbr?["roughnessFactor"]?.GetValue<float>() ?? 1,
                EmissiveFactor = Vector3From(item["emissiveFactor"]),
                BaseColorTexture = TextureBinding(pbr?["baseColorTexture"]),
                MetallicRoughnessTexture = TextureBinding(pbr?["metallicRoughnessTexture"]),
                NormalTexture = TextureBinding(item["normalTexture"]),
                OcclusionTexture = TextureBinding(item["occlusionTexture"]),
                EmissiveTexture = TextureBinding(item["emissiveTexture"]),
                AlphaMode = item["alphaMode"]?.GetValue<string>() ?? "OPAQUE",
                AlphaCutoff = item["alphaCutoff"]?.GetValue<float>() ?? 0.5f,
                DoubleSided = item["doubleSided"]?.GetValue<bool>() ?? false
            };
            Metadata(data, item);
            model.Materials.Add(data);
        }
    }

    private static void ReadAnimations(ModelData model, JsonObject root)
    {
        foreach (var item in Objects(root, "animations"))
        {
            var data = new ModelAnimationData();
            Metadata(data, item);
            foreach (var sampler in Objects(item, "samplers"))
            {
                var interpolation = sampler["interpolation"]?.GetValue<string>() ?? "LINEAR";
                var sample = new ModelAnimationSamplerData
                {
                    InputAccessorIndex = Int(sampler, "input"), OutputAccessorIndex = Int(sampler, "output"),
                    Interpolation = interpolation switch
                    {
                        "LINEAR" => ModelAnimationInterpolation.Linear, "STEP" => ModelAnimationInterpolation.Step,
                        "CUBICSPLINE" => ModelAnimationInterpolation.CubicSpline,
                        _ => throw new NotSupportedException($"Unsupported animation interpolation '{interpolation}'.")
                    }
                };
                Metadata(sample, sampler);
                data.Samplers.Add(sample);
            }

            foreach (var channel in Objects(item, "channels"))
            {
                var target = channel["target"]!.AsObject();
                var sample = new ModelAnimationChannelData
                {
                    SamplerIndex = Int(channel, "sampler"), NodeIndex = Int(target, "node"),
                    Path = target["path"]?.GetValue<string>() ?? string.Empty
                };
                Metadata(sample, channel);
                // Preserve target extensions such as KHR_animation_pointer with their target, not the channel.
                sample.TargetExtensions = target["extensions"]?.DeepClone().AsObject() ?? [];
                data.Channels.Add(sample);
            }

            model.Animations.Add(data);
        }
    }

    private static ModelTextureBindingData? TextureBinding(JsonNode? item)
    {
        if (item is not JsonObject source)
        {
            return null;
        }

        var data = new ModelTextureBindingData
        {
            TextureIndex = Int(source, "index"), TextureCoordinate = Int(source, "texCoord", 0),
            Scale = source["scale"]?.GetValue<float>() ?? source["strength"]?.GetValue<float>() ?? 1
        };
        Metadata(data, source);
        return data;
    }

    private static void Metadata(ModelElementData data, JsonObject item)
    {
        data.Name = item["name"]?.GetValue<string>() ?? string.Empty;
        data.Extras = item["extras"]?.DeepClone();
        data.Extensions = item["extensions"]?.DeepClone().AsObject() ?? [];
    }

    private static IEnumerable<JsonObject> Objects(JsonObject item, string key)
    {
        return item[key]?.AsArray().Select(n => n!.AsObject()) ?? [];
    }

    private static int Int(JsonObject item, string key, int fallback = -1) => item[key]?.GetValue<int>() ?? fallback;

    private static Vector3 Vector3From(JsonNode? value, Vector3 fallback = default)
    {
        return value == null
            ? fallback
            : new Vector3(value[0]!.GetValue<float>(), value[1]!.GetValue<float>(), value[2]!.GetValue<float>());
    }

    private static Vector4 Vector4From(JsonNode? value, Vector4 fallback)
    {
        return value == null
            ? fallback
            : new Vector4(
                value[0]!.GetValue<float>(),
                value[1]!.GetValue<float>(),
                value[2]!.GetValue<float>(),
                value[3]!.GetValue<float>()
            );
    }

    private static Matrix MatrixFrom(System.Numerics.Matrix4x4 m)
    {
        return new Matrix(
            m.M11,
            m.M12,
            m.M13,
            m.M14,
            m.M21,
            m.M22,
            m.M23,
            m.M24,
            m.M31,
            m.M32,
            m.M33,
            m.M34,
            m.M41,
            m.M42,
            m.M43,
            m.M44
        );
    }
}
