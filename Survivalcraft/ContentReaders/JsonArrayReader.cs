using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.ContentReaders;

public class JsonArrayReader : IContentReader
{
    public override string Type => "JsonArray";

    public override string[] DefaultSuffix => ["json"];

    public override object Get(ContentInfo[] contents)
    {
        return JsonSerializer.Deserialize<JsonArray>(new StreamReader(contents[0].Duplicate()).ReadToEnd()) ??
               throw new InvalidOperationException("Cannot Deserialize to JsonArray");
    }
}
