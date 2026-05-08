using Engine.FileStorage;

namespace Engine.Media;

public class ModelData
{
    public readonly List<ModelBoneData> Bones = [];

    public readonly List<ModelBuffersData> Buffers = [];

    public readonly List<ModelMeshData> Meshes = [];

    public static ModelFileFormat DetermineFileFormat(Stream stream)
    {
        return Collada.IsColladaStream(stream)
            ? ModelFileFormat.Collada
            : throw new InvalidOperationException("Unsupported model file format.");
    }

    public static ModelFileFormat DetermineFileFormat(string extension)
    {
        return extension.Equals(".dae", StringComparison.OrdinalIgnoreCase)
            ? ModelFileFormat.Collada
            : throw new InvalidOperationException("Unsupported model file format.");
    }

    public static ModelData Load(Stream stream, ModelFileFormat format)
    {
        return format == ModelFileFormat.Collada
            ? Collada.Load(stream)
            : throw new InvalidOperationException("Unsupported model file format.");
    }

    public static ModelData Load(string fileName, ModelFileFormat format)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream, format);
    }

    public static ModelData Load(Stream stream)
    {
        var peekStream = new PeekStream(stream, 256);
        var format = DetermineFileFormat(peekStream.GetInitialBytesStream());
        return Load(peekStream, format);
    }

    public static ModelData Load(string fileName)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream);
    }
}
