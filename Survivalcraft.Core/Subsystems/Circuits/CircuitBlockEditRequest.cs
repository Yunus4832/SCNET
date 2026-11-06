namespace Game.Subsystems.Circuits;

public readonly record struct CircuitBlockEditRequest(
    ComponentCharacter Character,
    Point3 Point,
    int OriginalValue,
    BuiltInBlockEditKind Kind
);
