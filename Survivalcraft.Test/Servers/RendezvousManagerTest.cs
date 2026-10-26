using System.Xml.Linq;

using Game.Managers;
using Game.Servers;

namespace Survivalcraft.Test.Servers;

public sealed class RendezvousManagerTest
{
    [Fact]
    public void InstalledListRoundTripsAndRejectsCanonicalDuplicateAddresses()
    {
        var first = new InstalledRendezvousService
        {
            Name = "Signal & Friends",
            ControlUrl = "https://signal.example/",
            RegistrationId = "registration",
            Order = 0
        };
        var second = new InstalledRendezvousService
        {
            Name = "Private",
            ControlUrl = "https://other.example/",
            IsEnabled = false,
            Order = 1
        };
        var settings = new RendezvousSettings { Services = [first, second] };
        var parsed = RendezvousManager.Read(XElement.Parse(RendezvousManager.Write(settings).ToString()));
        Assert.Equal(settings.Services, parsed.Services);
        var manager = new RendezvousManager(settings, _ => { });
        Assert.Throws<ArgumentException>(() => manager.Add(
                new InstalledRendezvousService
                {
                    Name = "Duplicate",
                    ControlUrl = "https://SIGNAL.example:443"
                }
            )
        );
        Assert.Equal(2, manager.Snapshot().Count);
    }

    [Fact]
    public void FailedSaveDoesNotChangeInMemoryConfiguration()
    {
        var manager = new RendezvousManager(new RendezvousSettings(), _ => throw new IOException("No space"));
        Assert.Throws<IOException>(() => manager.Add(
                new InstalledRendezvousService
                {
                    Name = "Signal",
                    ControlUrl = "https://signal.example/"
                }
            )
        );
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void ReorderRequiresCompleteUniqueListAndDeletionOnlyChangesInstalledList()
    {
        var first = new InstalledRendezvousService { Name = "A", ControlUrl = "https://a.example/", Order = 0 };
        var second = new InstalledRendezvousService { Name = "B", ControlUrl = "https://b.example/", Order = 1 };
        var saves = new List<RendezvousSettings>();
        var manager = new RendezvousManager(new RendezvousSettings { Services = [first, second] }, saves.Add);
        Assert.Throws<ArgumentException>(() => manager.SetOrder([first.Id, first.Id]));
        Assert.Empty(saves);
        manager.SetOrder([second.Id, first.Id]);
        Assert.Equal(second.Id, manager.Snapshot()[0].Id);
        manager.Delete(first.Id);
        Assert.Single(manager.Snapshot());
        Assert.Equal("https://a.example/", first.ControlUrl);
        Assert.Equal(2, saves.Count);
    }
}
