using System.Text.Json;

namespace Game.Utils;

public static class JsonUtils
{
    public static T? Deserialize<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<T>(json);
    }
}
