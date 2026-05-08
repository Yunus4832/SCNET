using Engine.Graphics;

namespace Game;

public class Subtexture(Texture2D texture, Vector2 topLeft, Vector2 bottomRight)
{
    public readonly Vector2 BottomRight = bottomRight;

    public readonly Texture2D Texture = texture;

    public readonly Vector2 TopLeft = topLeft;
}
