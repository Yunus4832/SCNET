using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.AssetReaders;

public class JsonObjectReader : AssetReader
{
    public override string Type => "JsonObject";

    public override string[] DefaultSuffix => ["json"];

    public override object Get(AssetInfo[] contents)
    {
        return JsonSerializer.Deserialize<JsonObject>(new StreamReader(contents[0].Duplicate()).ReadToEnd()) ??
               throw new InvalidOperationException("Cannot Deserialize to JsonObject");
    }
}
