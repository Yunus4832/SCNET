namespace Engine.Media.Models;

public sealed class ModelSamplerData : ModelElementData
{
    /// <summary>采用 glTF/OpenGL 的过滤参数值；为空表示未指定。</summary>
    public int? MinFilter;

    public int? MagFilter;

    public int WrapS = 10497;

    public int WrapT = 10497;
}
