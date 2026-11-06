namespace Game.Blocks;

public readonly record struct BlockDigOutcome(
    int OldValue,
    int NewValue,
    CellFace CellFace,
    Vector3 HitPosition,
    BlockDigOutcomeKind Kind,
    bool DebrisAllowed
);
