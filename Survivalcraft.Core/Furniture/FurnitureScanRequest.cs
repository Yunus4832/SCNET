namespace Game.Furniture;

public readonly record struct FurnitureScanRequest(CellFace Start, Vector3 Direction, ComponentMiner Miner);
