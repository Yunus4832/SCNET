using System.Text.Json.Nodes;

namespace Engine.Media.Models;

/// <summary>Asset metadata; names are optional labels, never object identities.</summary>
public abstract class ModelElementData
{
    public string Name = string.Empty;

    public JsonNode? Extras;

    public JsonObject Extensions = [];
}
