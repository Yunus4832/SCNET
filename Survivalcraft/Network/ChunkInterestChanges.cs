namespace Game.Network;

public readonly record struct ChunkInterestChanges(IReadOnlyList<Point2> Entered, IReadOnlyList<Point2> Left);
