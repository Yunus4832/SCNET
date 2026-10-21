using System.Text.Json.Nodes;

namespace Engine.Media.Models;

public sealed class ModelAnimationChannelData : ModelElementData
{
    public int SamplerIndex;

    public int NodeIndex = -1;

    public string Path = string.Empty;

    public JsonObject TargetExtensions = [];
}
