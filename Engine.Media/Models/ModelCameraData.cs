using System.Text.Json.Nodes;

namespace Engine.Media.Models;

public sealed class ModelCameraData : ModelElementData
{
    public string Type = string.Empty;

    /// <summary>Standard perspective or orthographic projection parameters.</summary>
    public JsonObject Projection = [];
}
