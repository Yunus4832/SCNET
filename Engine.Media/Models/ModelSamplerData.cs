namespace Engine.Media.Models;

public sealed class ModelSamplerData : ModelElementData
{
    /// <summary>glTF/OpenGL filter values; null leaves the filter unspecified.</summary>
    public int? MinFilter;

    public int? MagFilter;

    public int WrapS = 10497;

    public int WrapT = 10497;
}
