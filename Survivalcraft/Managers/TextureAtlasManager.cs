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
        var texture = ContentManager.Get<Texture2D>("Atlases/AtlasTexture");
        var s = ContentManager.Get<string>("Atlases/Atlas");
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
            value = new Subtexture((ContentManager.Get(typeof(Texture2D), name) as Texture2D)!, Vector2.Zero,
                Vector2.One);
            _subtextures.Add(name, value);
            return value;
        }
        catch (Exception innerException)
        {
            throw new InvalidOperationException($"Required subtexture {name} not found in TextureAtlasManager.",
                innerException);
        }
    }

    private static void LoadTextureAtlas(Texture2D texture, string atlasDefinition, string prefix)
    {
        var array = atlasDefinition.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var num = 0;
        while (true)
        {
            if (num >= array.Length)
            {
                return;
            }

            var array2 = array[num].Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (array2.Length < 5)
            {
                break;
            }

            var key = prefix + array2[0];
            var num2 = int.Parse(array2[1], CultureInfo.InvariantCulture);
            var num3 = int.Parse(array2[2], CultureInfo.InvariantCulture);
            var num4 = int.Parse(array2[3], CultureInfo.InvariantCulture);
            var num5 = int.Parse(array2[4], CultureInfo.InvariantCulture);
            var topLeft = new Vector2(num2 / (float)texture.Width, num3 / (float)texture.Height);
            var bottomRight = new Vector2((num2 + num4) / (float)texture.Width,
                (num3 + num5) / (float)texture.Height);
            var value = new Subtexture(texture, topLeft, bottomRight);
            _subtextures.Add(key, value);
            num++;
        }

        throw new InvalidOperationException("Invalid texture atlas definition.");
    }
}
