using System.Xml.Linq;

using Engine.Core;
using Engine.FileStorage;

using Game;
using Game.Managers;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class RunningSettingManagerTest : IDisposable
{
    private readonly FileBackup _runningSettingBackup = FileBackup.Create(RunningSettingManager.RunningSettingPath);

    [Fact]
    public void LoadUsesPendingSessionBeforeDefaultSession()
    {
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            DefaultSessionId = "default-session",
            PendingSessionId = "restart-123",
            DefaultGuiStartupBehavior = GuiStartupBehavior.MainMenu
        });

        var setting = RunningSettingManager.Load([]);

        Assert.Equal("default-session", setting.DefaultSessionId);
        Assert.Equal("restart-123", setting.PendingSessionId);
        Assert.Equal("restart-123", setting.ActiveSessionId);
        Assert.True(setting.ShouldEnterSession);
        Assert.True(setting.SessionIsTransient);
    }

    [Fact]
    public void LoadTreatsExplicitSessionWithoutSaveAsTransient()
    {
        SaveRunningSetting(new RunningSetting
        {
            DefaultSessionId = "default-session",
            DefaultGuiStartupBehavior = GuiStartupBehavior.MainMenu
        });

        var setting = RunningSettingManager.Load(["--session", "alpha", "--world", "CustomWorld", "--seed", "42"]);

        Assert.Equal("default-session", setting.DefaultSessionId);
        Assert.Equal("alpha", setting.ActiveSessionId);
        Assert.True(setting.HasExplicitSessionRequest);
        Assert.True(setting.ShouldEnterSession);
        Assert.True(setting.SessionIsTransient);
        Assert.Equal("CustomWorld", setting.SessionWorldOverride);
        Assert.Equal("42", setting.SessionSeedOverride);
    }

    [Fact]
    public void LoadPersistsDefaultSessionWhenExplicitSessionIsSaved()
    {
        SaveRunningSetting(new RunningSetting
        {
            DefaultSessionId = "default-session",
            DefaultGuiStartupBehavior = GuiStartupBehavior.MainMenu
        });

        var setting = RunningSettingManager.Load(["--session", "saved-session", "--save"]);

        Assert.Equal("saved-session", setting.DefaultSessionId);
        Assert.Equal("saved-session", setting.ActiveSessionId);
        Assert.False(setting.SessionIsTransient);

        var reloaded = RunningSettingManager.Load([]);
        Assert.Equal("saved-session", reloaded.DefaultSessionId);
    }

    public void Dispose()
    {
        _runningSettingBackup.Dispose();
    }

    private static void SaveRunningSetting(RunningSetting setting)
    {
        EnsureConfigDirectory();
        using var stream = Storage.OpenFile(RunningSettingManager.RunningSettingPath, OpenFileMode.Create);
        var root = new XElement("RunningSetting",
            new XAttribute(nameof(RunningSetting.RunMode), setting.RunMode),
            new XAttribute(nameof(RunningSetting.LogLevel), setting.LogLevel),
            new XAttribute(nameof(RunningSetting.DefaultSessionId), setting.DefaultSessionId),
            new XAttribute(nameof(RunningSetting.PendingSessionId), setting.PendingSessionId),
            new XAttribute(nameof(RunningSetting.DefaultGuiStartupBehavior), setting.DefaultGuiStartupBehavior));
        root.Save(stream);
    }

    private static void EnsureConfigDirectory()
    {
        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }
    }

    private sealed class FileBackup : IDisposable
    {
        private readonly string _path;
        private readonly string _backupPath;
        private readonly bool _hadOriginal;

        private FileBackup(string path, string backupPath, bool hadOriginal)
        {
            _path = path;
            _backupPath = backupPath;
            _hadOriginal = hadOriginal;
        }

        public static FileBackup Create(string path)
        {
            var backupPath = Storage.CombinePaths(GamePaths.Config, $"{Guid.NewGuid():N}.bak");
            var hadOriginal = Storage.FileExists(path);
            if (hadOriginal)
            {
                EnsureParentDirectory(backupPath);
                Storage.CopyFile(path, backupPath);
            }

            return new FileBackup(path, backupPath, hadOriginal);
        }

        public void Dispose()
        {
            if (Storage.FileExists(_path))
            {
                Storage.DeleteFile(_path);
            }

            if (_hadOriginal && Storage.FileExists(_backupPath))
            {
                EnsureParentDirectory(_path);
                Storage.CopyFile(_backupPath, _path);
                Storage.DeleteFile(_backupPath);
            }
        }

        private static void EnsureParentDirectory(string path)
        {
            var directory = Storage.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
            {
                Storage.CreateDirectory(directory);
            }
        }
    }
}
