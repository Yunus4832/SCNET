using Engine.Core;

namespace ServerLoadTool;

public sealed record LoadPosition(float X, float Y, float Z)
{
    // Engine vectors expose swizzle properties; serialize only the actual coordinates.
    public static LoadPosition? From(Vector3? position) =>
        position.HasValue ? new LoadPosition(position.Value.X, position.Value.Y, position.Value.Z) : null;
}
