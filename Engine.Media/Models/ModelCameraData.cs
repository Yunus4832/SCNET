using System.Text.Json.Nodes;

namespace Engine.Media.Models;

public sealed class ModelCameraData : ModelElementData
{
    public string Type = string.Empty;

    /// <summary>标准透视或正交投影参数。</summary>
    public JsonObject Projection = [];
}
