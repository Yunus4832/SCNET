namespace Rendezvous.Protocol;

public sealed record RoomPublication(string Name, string Description, bool IsPublic)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Length <= RendezvousLimits.MaximumRoomNameLength && Description is { Length: <= RendezvousLimits.MaximumRoomDescriptionLength };
}
