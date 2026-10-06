using EntitySystem.Core;

using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Network;

public static class NetworkSender
{
    public static void SendToServer(IPackage package)
    {
        if (CommonLib.WorkType != WorkType.Client || CommonLib.Net.Server is not { } server)
        {
            throw new InvalidOperationException("Only a network client can submit a package to its server.");
        }

        CommonLib.Net.QueuePackage(package, PackageAudience.To(server));
    }

    public static void SendGlobal(IPackage package)
    {
        CommonLib.Net.QueuePackage(package, PackageAudience.Global);
    }

    public static void SendTo(Client client, IPackage package)
    {
        CommonLib.Net.QueuePackage(package, PackageAudience.To(client));
    }

    public static void SendNearPoint(Project project, Vector3 position, float radius, IPackage package)
    {
        var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var observers = interest.GetObserversWithin(position.XZ, radius).ToArray();
        if (observers.Length > 0)
        {
            CommonLib.Net.QueuePackage(package, PackageAudience.To(observers));
        }
    }

    public static void SendToChunkObservers(Project project, Point2 chunk, IPackage package)
    {
        var observers = project.FindSubsystem<SubsystemNetworkInterest>(true)!.GetChunkObservers(chunk).ToArray();
        if (observers.Length > 0)
        {
            CommonLib.Net.QueuePackage(package, PackageAudience.To(observers));
        }
    }

    public static void SendToObservers(Entity entity, IPackage package, Client? except = null)
    {
        var body = entity.FindComponent<ComponentBody>();
        if (body is null)
        {
            throw new InvalidOperationException(
                $"Entity {entity.EntityId} has no body and cannot be routed by entity position.");
        }

        var interest = entity.Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var candidates = interest.Entities.GetObservers(EntityInterestGroup.Creatures, entity.EntityId);
        if (body.Player?.PlayerData.Client is { IsConnected: true } owner)
        {
            candidates = candidates.Append(owner);
        }

        var observers = candidates
            .Where(client => client.IsConnected)
            .Where(client => !ReferenceEquals(client, except))
            .Distinct<Client>(ReferenceEqualityComparer.Instance)
            .ToArray();
        if (observers.Length > 0)
        {
            if (package is ComponentMountPackage { Type: ComponentMountPackage.EventType.Mount })
            {
                // A newly spawned mount may not have entered the periodic baseline yet.
                // Queue missing dependencies on Bulk before the ordered mount event.
                foreach (var observer in observers)
                {
                    interest.EnsureBodyGroupObserved(observer, body);
                }
            }

            CommonLib.Net.QueuePackage(package, PackageAudience.To(observers));
        }
    }
}
