namespace Game.Subsystems;

public readonly record struct SignInteractionRequest(
    ComponentCharacter Character,
    Point3 Point,
    SignInteractionKind Kind
);
