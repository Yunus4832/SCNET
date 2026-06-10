namespace Game.Utils;

public static class StreamUtils
{
    public static string ReadString(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        return new StreamReader(stream).ReadToEnd();
    }

    public static byte[] ReadBytes(Stream stream)
    {
        var bytes = new byte[stream.Length];
        stream.Seek(0, SeekOrigin.Begin);
        stream.ReadExactly(bytes, 0, bytes.Length);
        return bytes;
    }
}
