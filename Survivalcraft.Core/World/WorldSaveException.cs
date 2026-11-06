namespace Game.World;

public sealed class WorldSaveException(string worldName, Exception innerException)
    : IOException($"Failed to save world \"{worldName}\".", innerException);
