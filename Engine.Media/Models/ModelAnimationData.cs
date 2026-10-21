namespace Engine.Media.Models;

public sealed class ModelAnimationData : ModelElementData
{
    public readonly List<ModelAnimationSamplerData> Samplers = [];

    public readonly List<ModelAnimationChannelData> Channels = [];
}
