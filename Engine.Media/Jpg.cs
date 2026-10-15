namespace Engine.Media;

public static class Jpg
{
    public static bool IsJpgStream(Stream stream) => ImageCodec.Identify(stream).Format == ImageFormat.Jpeg;

    public static Image Load(Stream stream) => Image.Load(stream, ImageFileFormat.Jpg);

    public static void Save(Image image, Stream stream, int quality) =>
        ImageCodec.Encode(image.Buffer, stream, ImageFormat.Jpeg, false, quality);
}
