namespace ServerSource.Protocol;

public static class ServerSourceProtocol
{
    public const int CurrentVersion = 1;

    public const int DefaultPageSize = 100;

    public const int MaximumPageSize = 100;

    public const int MaximumResponseBytes = 1024 * 1024;

    public const int MaximumSourceIdLength = 64;

    public const int MaximumEntryIdLength = 128;

    public const int MaximumNameLength = 100;

    public const int MaximumAddressLength = 255;

    public const int MaximumDescriptionLength = 1024;

    public const int MaximumTagCount = 16;

    public const int MaximumTagLength = 32;

    public const int MaximumCursorLength = 512;
}
