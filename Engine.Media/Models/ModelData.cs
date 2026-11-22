using Engine.Media.Models.Formats;

namespace Engine.Media.Models;

/// <summary>与图形设备无关的场景资源；所有引用均使用集合索引。</summary>
public sealed class ModelData : ModelElementData
{
    public readonly List<ModelSceneData> Scenes = [];

    public readonly List<ModelNodeData> Nodes = [];

    public readonly List<ModelMeshData> Meshes = [];

    public readonly List<ModelAccessorData> Accessors = [];

    public readonly List<ModelMaterialData> Materials = [];

    public readonly List<ModelTextureData> Textures = [];

    public readonly List<ModelImageData> Images = [];

    public readonly List<ModelSamplerData> Samplers = [];

    public readonly List<ModelSkinData> Skins = [];

    public readonly List<ModelAnimationData> Animations = [];

    public readonly List<ModelCameraData> Cameras = [];

    public string[] ExtensionsUsed = [];

    public string[] ExtensionsRequired = [];

    /// <summary>值为 -1 表示资源未指定默认场景。</summary>
    public int DefaultSceneIndex = -1;

    public static ModelFileFormat DetermineFileFormat(string extension) => extension.ToLowerInvariant() switch
    {
        ".gltf" => ModelFileFormat.Gltf,
        ".glb" => ModelFileFormat.Glb,
        _ => throw new InvalidOperationException("Unsupported model file format.")
    };

    public static ModelFileFormat DetermineFileFormat(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Format inspection requires a seekable stream.", nameof(stream));
        }

        var position = stream.Position;
        try
        {
            Span<byte> signature = stackalloc byte[4];
            var count = stream.ReadAtLeast(signature, signature.Length, throwOnEndOfStream: false);
            if (count == 4 && signature.SequenceEqual("glTF"u8))
            {
                return ModelFileFormat.Glb;
            }

            stream.Position = position;
            using var reader = new StreamReader(stream, leaveOpen: true);
            int next;
            do
            {
                next = reader.Read();
            } while (next >= 0 && char.IsWhiteSpace((char)next));

            return next == '{'
                ? ModelFileFormat.Gltf
                : throw new InvalidDataException("Unsupported model file format.");
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <summary>解析器按资源相对路径打开流，并将返回的流交由加载器释放。</summary>
    public static ModelData Load(Stream stream, Func<string, Stream>? resourceResolver = null)
    {
        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        buffered.Position = 0;
        return GltfReader.Load(buffered, DetermineFileFormat(buffered), resourceResolver);
    }

    public static ModelData Load(Stream stream, ModelFileFormat format, Func<string, Stream>? resourceResolver = null)
    {
        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        buffered.Position = 0;
        return GltfReader.Load(buffered, format, resourceResolver);
    }
}
