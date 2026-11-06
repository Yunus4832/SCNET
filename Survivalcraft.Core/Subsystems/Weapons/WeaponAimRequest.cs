namespace Game.Subsystems;

public readonly record struct WeaponAimRequest(
    ComponentMiner Miner,
    Ray3 Aim,
    Vector3 ItemOffset,
    Vector3? ItemRotation
);
