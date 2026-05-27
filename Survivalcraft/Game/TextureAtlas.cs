using System.Globalization;

using Engine.Graphics;

namespace Game;

public class TextureAtlas
{
    private readonly Dictionary<string, Rectangle> _rectangles = new();

    public TextureAtlas(Texture2D texture, string atlasDefinition, string prefix)
    {
        Texture = texture;
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
            var value = new Rectangle
            {
                Left = int.Parse(array2[1], CultureInfo.InvariantCulture),
                Top = int.Parse(array2[2], CultureInfo.InvariantCulture),
                Width = int.Parse(array2[3], CultureInfo.InvariantCulture),
                Height = int.Parse(array2[4], CultureInfo.InvariantCulture)
            };
            _rectangles.Add(key, value);
            num++;
        }

        throw new InvalidOperationException("Invalid texture atlas definition.");
    }

    public Texture2D Texture { get; }

    public bool ContainsTexture(string textureName)
    {
        return _rectangles.ContainsKey(textureName);
    }

    public Vector4? GetTextureCoordinates(string textureName)
    {
        if (!_rectangles.TryGetValue(textureName, out var value))
        {
            return null;
        }

        Vector4 value2 = default;
        value2.X = value.Left / (float)Texture.Width;
        value2.Y = value.Top / (float)Texture.Height;
        value2.Z = value.Right / (float)Texture.Width;
        value2.W = value.Bottom / (float)Texture.Height;
        return value2;

    }
}
