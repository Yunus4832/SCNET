namespace Game.World;

public readonly record struct PaintEffectRequest(Vector3 Position, Vector3 Normal, int? ColorIndex);
