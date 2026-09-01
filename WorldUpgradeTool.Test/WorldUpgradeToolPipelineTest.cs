using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace WorldUpgradeTool.Test;

public sealed class WorldUpgradeToolPipelineTest
{
    [Fact]
    public void UpgradeWorld_RemovesLegacyTerritorySubsystemFromOutputCopy()
    {
        using var tempWorld = TempWorld.Create();
        tempWorld.WriteProjectXml(
            """
            <Project Version="SCNET-1">
              <Subsystems>
                <Values Name="GameInfo">
                  <Value Name="WorldName" Type="string" Value="Legacy Territory Test" />
                </Values>
                <Values Name="BedrockBlockBehavior">
                  <Values Name="Territoriy">
                    <Values Name="0">
                      <Value Name="Guid" Type="Guid" Value="11111111-1111-1111-1111-111111111111" />
                      <Value Name="OwnChunkCoord" Type="Point3" Value="1,64,2" />
                      <Value Name="AllowDig" Type="bool" Value="True" />
                      <Value Name="AllowPlace" Type="bool" Value="True" />
                      <Value Name="AllowTeamEnter" Type="bool" Value="True" />
                      <Value Name="OwnerTeamId" Type="Guid" Value="22222222-2222-2222-2222-222222222222" />
                      <Value Name="IsVisible" Type="bool" Value="True" />
                    </Values>
                  </Values>
                </Values>
              </Subsystems>
            </Project>
            """);

        using var outputWorld = TempWorld.ReserveSibling(tempWorld.DirectoryPath, ".Upgraded");
        var upgradedWorldDirectory = WorldUpgradeManager.UpgradeWorld(tempWorld.StoragePath, outputWorld.StoragePath);

        var sourceProject = tempWorld.LoadProjectXml();
        var sourceSubsystemNames = sourceProject.Element("Subsystems")!.Elements("Values")
            .Select(e => (string?)e.Attribute("Name"))
            .ToArray();
        Assert.Contains("BedrockBlockBehavior", sourceSubsystemNames);

        var project = outputWorld.LoadProjectXml();
        Assert.Equal(outputWorld.StoragePath, upgradedWorldDirectory);
        var subsystemNames = project.Element("Subsystems")!.Elements("Values")
            .Select(e => (string?)e.Attribute("Name"))
            .ToArray();
        Assert.DoesNotContain("TerritoryBlockBehavior", subsystemNames);
        Assert.DoesNotContain("BedrockBlockBehavior", subsystemNames);
        Assert.Equal("SCNET-1", (string?)project.Attribute("Version"));
        var upgradedProjectText = File.ReadAllText(outputWorld.ProjectXmlPath);
        Assert.DoesNotContain("ApplyToFriend", upgradedProjectText);
        Assert.DoesNotContain("AllowTeamEnter", upgradedProjectText);
        Assert.DoesNotContain("OwnerTeamId", upgradedProjectText);
    }

    [Fact]
    public void UpgradeWorld_ConvertsLegacyProjectJsonBeforeRepairs()
    {
        using var tempWorld = TempWorld.Create();
        tempWorld.WriteProjectJson(
            """
            {
              "Version": ["string", "SCNET-1"],
              "Subsystems": {
                "GameInfo": {
                  "WorldName": ["string", "Json World"]
                }
              }
            }
            """);

        using var outputWorld = TempWorld.ReserveSibling(tempWorld.DirectoryPath, ".Upgraded");
        WorldUpgradeManager.UpgradeWorld(tempWorld.StoragePath, outputWorld.StoragePath);

        Assert.False(File.Exists(tempWorld.ProjectXmlPath));
        Assert.True(File.Exists(outputWorld.ProjectXmlPath));
        Assert.False(File.Exists(Path.Combine(outputWorld.DirectoryPath, "Project.json")));
        var project = outputWorld.LoadProjectXml();
        Assert.Equal("SCNET-1", (string?)project.Attribute("Version"));
        Assert.Equal("Json World", ReadGameInfoValue(project, "WorldName"));
    }

    [Fact]
    public void UpgradeWorld_EmptyOutputDirectoryUsesSiblingUpgradedDirectory()
    {
        using var tempWorld = TempWorld.Create();
        tempWorld.WriteProjectXml(
            """
            <Project Version="SCNET-1">
              <Subsystems>
                <Values Name="GameInfo">
                  <Value Name="WorldName" Type="string" Value="Default Output Test" />
                </Values>
              </Subsystems>
            </Project>
            """);

        var outputDirectoryPath = tempWorld.DirectoryPath + ".Upgraded";
        if (Directory.Exists(outputDirectoryPath))
        {
            Directory.Delete(outputDirectoryPath, true);
        }

        try
        {
            var upgradedWorldDirectory = WorldUpgradeManager.UpgradeWorld(tempWorld.StoragePath, string.Empty);

            Assert.Equal(tempWorld.StoragePath + ".Upgraded", upgradedWorldDirectory);
            Assert.True(File.Exists(Path.Combine(outputDirectoryPath, "Project.xml")));
            Assert.Equal("SCNET-1", (string?)XElement.Load(Path.Combine(outputDirectoryPath, "Project.xml"))
                .Attribute("Version"));
        }
        finally
        {
            if (Directory.Exists(outputDirectoryPath))
            {
                Directory.Delete(outputDirectoryPath, true);
            }
        }
    }

    [Fact]
    public void UpgradeWorld_UpgradesTerritoryRefactorAssetWithoutMutatingAsset()
    {
        using var outputWorld = UpgradeAssetWorldWithoutMutatingSource("WorldBeforeTerritoryBlockRefactor");
        var upgradedProjectText = File.ReadAllText(outputWorld.ProjectXmlPath);

        Assert.DoesNotContain("BedrockBlockBehavior", upgradedProjectText);
        Assert.DoesNotContain("AllowTeamEnter", upgradedProjectText);
        Assert.DoesNotContain("OwnerTeamId", upgradedProjectText);
        Assert.DoesNotContain("<Values Name=\"Teams\"", upgradedProjectText);
    }

    [Fact]
    public void UpgradeWorld_UpgradesLegacyJsonAssetWithoutMutatingAsset()
    {
        using var outputWorld = UpgradeAssetWorldWithoutMutatingSource("WorldUseJson");
        var upgradedProjectText = File.ReadAllText(outputWorld.ProjectXmlPath);

        Assert.False(File.Exists(Path.Combine(GetAssetWorldDirectory("WorldUseJson"), "Project.xml")));
        Assert.False(File.Exists(Path.Combine(outputWorld.DirectoryPath, "Project.json")));
        Assert.DoesNotContain("BedrockBlockBehavior", upgradedProjectText);
        Assert.DoesNotContain("TerritoryBlockBehavior", upgradedProjectText);
        Assert.DoesNotContain("ApplyToFriend", upgradedProjectText);
        Assert.DoesNotContain("AllowTeamEnter", upgradedProjectText);
        Assert.DoesNotContain("OwnerTeamId", upgradedProjectText);
        Assert.DoesNotContain("<Values Name=\"Teams\"", upgradedProjectText);
    }

    private static string? ReadGameInfoValue(XElement project, string name)
    {
        var gameInfo = project.Element("Subsystems")?.Elements("Values")
            .FirstOrDefault(e => (string?)e.Attribute("Name") == "GameInfo");
        return (string?)gameInfo?.Elements("Value")
            .FirstOrDefault(e => (string?)e.Attribute("Name") == name)
            ?.Attribute("Value");
    }

    private sealed class TempWorld : IDisposable
    {
        private readonly bool _deleteOnDispose;

        private TempWorld(string directoryPath, bool deleteOnDispose)
        {
            DirectoryPath = directoryPath;
            StoragePath = "system:" + directoryPath;
            ProjectXmlPath = Path.Combine(directoryPath, "Project.xml");
            _deleteOnDispose = deleteOnDispose;
        }

        public string DirectoryPath { get; }

        public string StoragePath { get; }

        public string ProjectXmlPath { get; }

        public static TempWorld Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "scnet-world-upgrade-test-" + Guid.NewGuid());
            Directory.CreateDirectory(directoryPath);
            return new TempWorld(directoryPath, true);
        }

        public static TempWorld ReserveSibling(string sourceDirectoryPath, string suffix, bool deleteOnDispose = true)
        {
            var directoryPath = sourceDirectoryPath + suffix;
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }

            return new TempWorld(directoryPath, deleteOnDispose);
        }

        public void WriteProjectXml(string text)
        {
            File.WriteAllText(ProjectXmlPath, text);
        }

        public void WriteProjectJson(string text)
        {
            File.WriteAllText(Path.Combine(DirectoryPath, "Project.json"), text);
        }

        public XElement LoadProjectXml()
        {
            return XElement.Load(ProjectXmlPath);
        }

        public void Dispose()
        {
            if (_deleteOnDispose && Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    private static string ToStoragePath(string path) =>
        "system:" + Path.GetFullPath(path);

    private static string FindAssetWorldsDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Worlds");
    }

    private static string GetAssetWorldDirectory(string worldName)
    {
        return Path.Combine(FindAssetWorldsDirectory(), worldName);
    }

    private static TempWorld UpgradeAssetWorldWithoutMutatingSource(string worldName)
    {
        var assetWorldDirectory = GetAssetWorldDirectory(worldName);
        var sourceProjectPath = GetProjectPath(assetWorldDirectory);
        var sourceProjectText = File.ReadAllText(sourceProjectPath);
        var outputWorld = TempWorld.ReserveSibling(assetWorldDirectory, ".Upgraded", deleteOnDispose: false);

        WorldUpgradeManager.UpgradeWorld(ToStoragePath(assetWorldDirectory), outputWorld.StoragePath);

        Assert.Equal(sourceProjectText, File.ReadAllText(sourceProjectPath));
        Assert.True(File.Exists(outputWorld.ProjectXmlPath));

        var project = outputWorld.LoadProjectXml();
        Assert.Equal("SCNET-1", (string?)project.Attribute("Version"));
        AssertProjectSubsystemDictionariesAreInitialized(outputWorld.ProjectXmlPath);
        return outputWorld;
    }

    private static string GetProjectPath(string worldDirectory)
    {
        var xmlPath = Path.Combine(worldDirectory, "Project.xml");
        if (File.Exists(xmlPath))
        {
            return xmlPath;
        }

        var jsonPath = Path.Combine(worldDirectory, "Project.json");
        if (File.Exists(jsonPath))
        {
            return jsonPath;
        }

        throw new FileNotFoundException($"Project.xml or Project.json was not found in {worldDirectory}.");
    }

    private static void AssertProjectSubsystemDictionariesAreInitialized(string projectXmlPath)
    {
        var database = new GameDatabase(XmlDatabaseSerializer.LoadDatabase(XElement.Load(FindDatabasePath())));
        var projectData = new ProjectData(database, XElement.Load(projectXmlPath), null, true);
        foreach (var subsystemValues in projectData.ValuesDictionary.Values.OfType<ValuesDictionary>())
        {
            _ = subsystemValues.DatabaseObject;
        }
    }

    private static string FindDatabasePath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Content", "Assets", "Database.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Content/Assets/Database.xml was not found.");
    }
}
