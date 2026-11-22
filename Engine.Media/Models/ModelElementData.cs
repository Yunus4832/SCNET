using System.Text.Json.Nodes;

namespace Engine.Media.Models;

/// <summary>资源元数据。名称仅用作可选标签，不能标识对象。</summary>
public abstract class ModelElementData
{
    public string Name = string.Empty;

    public JsonNode? Extras;

    public JsonObject Extensions = [];
}
