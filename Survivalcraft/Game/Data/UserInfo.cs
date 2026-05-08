namespace Game;

public class UserInfo(string uniqueId, string displayName)
{
    public readonly string DisplayName = displayName;

    public readonly string UniqueId = uniqueId;
}
