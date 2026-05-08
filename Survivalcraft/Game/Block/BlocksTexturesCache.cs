using Engine.Graphics;

namespace Game;

public class BlocksTexturesCache
{
    public Dictionary<string, Texture2D> Textures = new();

    public Texture2D GetTexture(string name)
    {
        if (Textures.TryGetValue(name, out var value))
        {
            return value;
        }

        value = BlocksTexturesManager.LoadTexture(name);
        Textures.Add(name, value);

        return value;
    }

    public void Clear()
    {
        foreach (var value in Textures.Values.Where(value => !ContentManager.IsContent(value)))
        {
            value.Dispose();
        }

        Textures.Clear();
    }
}
