namespace Game.World;

public sealed record WorldSaveOutcome(Guid OperationId, string WorldName, Exception? Failure);
