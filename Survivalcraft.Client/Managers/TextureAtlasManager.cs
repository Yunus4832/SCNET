using System.Globalization;

using Engine.Graphics;

namespace Game.Managers;

public static class TextureAtlasManager
{
    private static readonly Dictionary<string, Subtexture> _subtextures = new();

    private static Texture2D _atlasTexture = null!;

    private static void Clear()
    {
        _subtextures.Clear();
    }

    public static void Initialize()
    {
        var texture = AssetManager.Get<Texture2D>("Atlases/AtlasTexture");
        var s = AssetManager.Get<string>("Atlases/Atlas");
        LoadAtlases(texture, s);
    }

    private static void LoadAtlases(Texture2D atlasTexture, string atlas)
    {
        Clear();
        _atlasTexture = atlasTexture;
        LoadTextureAtlas(_atlasTexture, atlas, "Textures/Atlas/");
    }

    public static Subtexture GetSubtexture(string name)
    {
        if (_subtextures.TryGetValue(name, out var value))
        {
            return value;
        }

        try
        {
            value = new Subtexture(
                (AssetManager.Get(typeof(Texture2D), name) as Texture2D)!,
                Vector2.Zero,
                Vector2.One
            );
            _subtextures.Add(name, value);
            return value;
        }
        catch (Exception innerException)
        {
            throw new InvalidOperationException(
                $"Required subtexture {name} not found in TextureAtlasManager.",
                innerException
            );
        }
    }

    public static bool TryGetSubtexture(string name, out Subtexture subtexture)
    {
        return _subtextures.TryGetValue(name, out subtexture!);
    }

    private static void LoadTextureAtlas(Texture2D texture, string atlasDefinition, string prefix)
    {
        var array = atlasDefinition.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var lineIndex = 0;
        while (true)
        {
            if (lineIndex >= array.Length)
            {
                return;
            }

            var array2 = array[lineIndex].Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (array2.Length < 5)
            {
                break;
            }

            var key = prefix + array2[0];
            var left = int.Parse(array2[1], CultureInfo.InvariantCulture);
            var top = int.Parse(array2[2], CultureInfo.InvariantCulture);
            var width = int.Parse(array2[3], CultureInfo.InvariantCulture);
            var height = int.Parse(array2[4], CultureInfo.InvariantCulture);
            var topLeft = new Vector2(left / (float)texture.Width, top / (float)texture.Height);
            var bottomRight = new Vector2(
                (left + width) / (float)texture.Width,
                (top + height) / (float)texture.Height
            );
            var value = new Subtexture(texture, topLeft, bottomRight);
            _subtextures.Add(key, value);
            lineIndex++;
        }

        throw new InvalidOperationException("Invalid texture atlas definition.");
    }
}
