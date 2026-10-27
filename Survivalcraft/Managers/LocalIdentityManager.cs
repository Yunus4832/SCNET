using System.Xml.Linq;

using Game.Identity;

namespace Game.Managers;

public static class LocalIdentityManager
{
    private static LocalIdentityConfiguration? _current;

    public static LocalIdentityConfiguration Current => _current ??
                                                        throw new InvalidOperationException(
                                                            "Local identities have not been initialized."
                                                        );

    public static void Initialize()
    {
        _current = null;
        var path = Storage.GetSystemPath(GamePaths.LocalIdentitiesFile);
        XElement? configuration = null;
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length > 8192)
            {
                throw new InvalidDataException("Identity configuration is too large.");
            }

            configuration = XElement.Load(path);
        }

        var identities = new LocalIdentityConfiguration(configuration, Save);
        identities.Initialize();
        _current = identities;
    }

    public static bool CanImport => GameManager.Project is null &&
                                    !Network.CommonLib.Net.IsTransportRunning &&
                                    Network.CommonLib.Net.CurrentStage == Network.NetNode.Stage.NotConnected;

    public static string ImportBackup(
        IdentityPurpose purpose,
        ReadOnlySpan<byte> backup,
        ReadOnlySpan<char> password,
        string? expectedAddress
    )
    {
        if (!CanImport)
        {
            throw new InvalidOperationException("Disconnect and leave the world before importing an identity.");
        }

        return Current.ImportBackup(purpose, backup, password, expectedAddress);
    }

    private static void Save(XElement configuration)
    {
        var path = Storage.GetSystemPath(GamePaths.LocalIdentitiesFile);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions
                { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                configuration.Save(stream);
                stream.Flush(true);
            }

            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }
}
