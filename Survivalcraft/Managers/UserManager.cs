namespace Game.Managers;

public static class UserManager
{
    private static readonly List<UserInfo> _users;

    static UserManager()
    {
        _users = [];
        string text;
        try
        {
            var path = GamePaths.UserData;
            if (!Storage.FileExists(path))
            {
                text = Guid.NewGuid().ToString();
                Storage.WriteAllText(path, text);
            }
            else
            {
                text = Storage.ReadAllText(path);
            }
        }
        catch (Exception)
        {
            text = Guid.NewGuid().ToString();
        }

        _users.Add(new UserInfo(text, "Windows User"));
    }

    public static UserInfo? ActiveUser
    {
        get => GetUser(SettingsManager.Current.UserId);
        set => SettingsManager.Current.UserId = value != null ? value.UniqueId : string.Empty;
    }

    public static IEnumerable<UserInfo> GetUsers()
    {
        return new ReadOnlyList<UserInfo>(_users);
    }

    private static UserInfo? GetUser(string uniqueId)
    {
        return GetUsers().FirstOrDefault(u => u.UniqueId == uniqueId);
    }
}
