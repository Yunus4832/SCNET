using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Game.Hosting;
using Game.Startup;

namespace Game.World;

/// <summary>Owns the actual loaded authority/client world runtime and its host loading/closing operations.</summary>
public sealed class GameWorldController(WorldProjectActivity activity) : IDisposable
{
    public GameRuntime? Runtime { get; private set; }

    public WorldInfo? WorldInfo { get; private set; }

    public event Action<Project>? AuthorityLoaded;

    public SessionInfo? WorldSession { get; private set; }

    public LocalGameReturnTarget? LocalPlayerReturnTarget { get; set; }

    public void LoadAuthority(WorldLoadRequest request)
    {
        if (Runtime?.Authority?.BoundProject is not null)
        {
            throw new InvalidOperationException("Close the existing authority world before loading another world.");
        }

        if (request.AuthorityNode.Role != NodeRole.Authority || request.AuthorityNode.IsTransportRunning)
        {
            throw new InvalidOperationException("Authority loading requires an idle authority endpoint.");
        }

        Close();
        if (request.World.ProjectFormatVersion != WorldVersions.ProjectFormatVersion)
        {
            throw new InvalidOperationException($"Unsupported project format '{request.World.ProjectFormatVersion}'.");
        }

        var runtime = request.AuthorityNode.PrepareWorldRuntime();
        var game = new GameRuntime(request.AllowsLocalPause);
        Runtime = game;
        WorldInfo = request.World;
        WorldSession = request.StartupSession;
        try
        {
            game.Attach(runtime);
            activity.Track(runtime, request.World.DirectoryName);
            var projectPath = Storage.CombinePaths(request.World.DirectoryName, "Project.xml");
            if (!Storage.FileExists(projectPath))
            {
                throw new FileNotFoundException("Project file not found.", projectPath);
            }

            XElement node;
            using (var stream = Storage.OpenFile(projectPath, OpenFileMode.Read))
            {
                node = XmlUtils.LoadXmlFromStream(stream, null, true);
            }

            var overrides = new ValuesDictionary
            {
                { "GameInfo", new ValuesDictionary { { "WorldDirectoryName", request.World.DirectoryName } } }
            };
            var catalog = request.Mods.CreateWorldCatalog(
                runtime,
                BlockIndexMapping.FromXml(node.Element("BlockRegistry"))
            );
            var project = AuthorityWorldProjectFactory.Create(
                runtime,
                catalog,
                request.Database,
                node,
                overrides,
                request.Policies,
                metadata => WorldProjectPersistence.SaveMetadata(request.World.DirectoryName, metadata)
            );
            if (request.World.GameModeOverride is { } mode)
            {
                project.FindSubsystem<SubsystemWorldHosting>(true)!.ApplyGameModeOverride(mode);
                request.World.WorldSettings.GameMode = mode;
            }

            project.FindSubsystem<SubsystemWorldHosting>(true)!.ApplyRunServerOverride(request.World.Hosting.RunServer);
            var characters = project.FindSubsystem<SubsystemCharacters>(true)!;
            foreach (var character in characters.Characters.ToArray())
            {
                characters.MakeCharacterOffline(character.CharacterId, false);
            }

            request.AuthorityNode.TurnOnPackageHandle(project);
            AuthorityWorldNetworkSetup.Attach(project, request.AuthorityNode, request.Textures);
            runtime.OwnWorldResource(new WorldTimeReplication(project));
            AuthorityLoaded?.Invoke(project);
        }
        catch
        {
            Close();
            throw;
        }
    }

    public GameRuntime RequireRuntime() => Runtime ?? throw new InvalidOperationException("No game runtime is loaded.");

    public void AttachClient(WorldRuntimeContext runtime)
    {
        Runtime ??= new GameRuntime(false);
        if (!ReferenceEquals(Runtime.Client, runtime)) { Runtime.ReleaseClient(); }

        Runtime.Attach(runtime);
    }

    public void Close()
    {
        WorldProjectPersistence.WaitForPendingSaves();
        var runtime = Runtime;
        try { runtime?.Dispose(); }
        finally
        {
            Runtime = null;
            WorldInfo = null;
            WorldSession = null;
            LocalPlayerReturnTarget = null;
        }
    }

    public void Dispose() => Close();
}
