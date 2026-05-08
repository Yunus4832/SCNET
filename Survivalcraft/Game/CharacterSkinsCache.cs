using Engine.Graphics;

namespace Game;

public class CharacterSkinsCache
{
    private readonly Dictionary<string, Texture2D> _textures = new();

    public bool ContainsTexture(Texture2D texture)
    {
        return _textures.ContainsValue(texture);
    }

    public Texture2D GetTexture(string name)
    {
        if (_textures.TryGetValue(name, out var value))
        {
            return value;
        }

        value = CharacterSkinsManager.LoadTexture(name)!;
        _textures.Add(name, value);

        return value;
    }

    public void Clear()
    {
        foreach (var value in _textures.Values.Where(value => !ContentManager.IsContent(value)))
        {
            value.Dispose();
        }

        _textures.Clear();
    }
}
