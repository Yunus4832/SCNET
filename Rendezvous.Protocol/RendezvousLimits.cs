namespace Rendezvous.Protocol;

public static class RendezvousLimits
{
    public const int MaximumControlRequestBytes = 16 * 1024;

    public const int MaximumControlResponseBytes = 1024 * 1024;

    public const int MaximumRoomNameLength = 100;

    public const int MaximumRoomDescriptionLength = 1024;

    public const int MaximumRoomPageSize = 100;

    public const int MaximumRoomPages = 100;

    public const int MaximumRoomCursorLength = 512;
}
