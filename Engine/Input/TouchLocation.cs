using Engine.Core.Mathematics;

namespace Engine.Input;

public struct TouchLocation
{
    public int Id;

    public Vector2 Position;

    public TouchLocationState State;

    internal bool releaseQueued;
}
