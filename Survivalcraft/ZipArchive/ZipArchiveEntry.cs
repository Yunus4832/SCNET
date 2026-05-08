namespace Game.ZipArchive;

public class ZipArchiveEntry
{
    public string Comment = string.Empty;

    public uint CompressedSize;

    public uint Crc32;

    public bool EncodeUTF8;

    public string FilenameInZip = string.Empty;

    public uint FileOffset;

    public uint FileSize;

    public uint HeaderOffset;

    public uint HeaderSize;

    public bool IsFilenameUtf8;

    public ZipArchive.Compression Method;

    public DateTime ModifyTime;

    public override string ToString()
    {
        return FilenameInZip;
    }
}
