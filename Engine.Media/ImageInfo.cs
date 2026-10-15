namespace Engine.Media;

public readonly record struct ImageInfo(int Width, int Height, ImageFormat Format, int Frames = 1);
