using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Celebrations;
using Game.Characters;
using Game.Components;
using Game.Configuration;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class NewYearCelebrationHostingTest
{
    [Fact]
    public void CalendarHostIsConstructedOnlyForAuthorityWithItsRuleDependency()
    {
        var document = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
        var database = XmlDatabaseSerializer.LoadDatabase(document);
        var member = database.FindDatabaseObject(
            Guid.Parse("977706ef-afa1-5513-b899-81a208a59450"),
            database.FindDatabaseObjectType("MemberSubsystemTemplate", true),
            true
        )!;
        var definition = new ValuesDictionary();
        definition.PopulateFromDatabaseObject(member);
        Assert.Equal(typeof(SubsystemNewYearCelebrationHosting).FullName, definition.GetValue<string>("Class"));
        Assert.Equal("FireworksBlockBehavior", definition.GetValue<string>("Dependencies"));
        Assert.False(definition.GetValue<bool>("IsOptional"));
        Assert.True(new WorldProjectComposition(RuntimeSide.Authority, false).ShouldCreateSubsystem(definition));
        Assert.True(new WorldProjectComposition(RuntimeSide.Authority, true).ShouldCreateSubsystem(definition));
        Assert.False(new WorldProjectComposition(RuntimeSide.Client, false).ShouldCreateSubsystem(definition));
        Assert.False(new WorldProjectComposition(RuntimeSide.Client, true).ShouldCreateSubsystem(definition));
    }

    [Theory]
    [InlineData(2027, 1, 1, 0, 0, 2026, true)]
    [InlineData(2027, 1, 1, 0, 58, 2026, true)]
    [InlineData(2027, 1, 1, 0, 59, 2026, false)]
    [InlineData(2027, 1, 1, 1, 0, 2026, false)]
    [InlineData(2027, 1, 2, 0, 0, 2026, false)]
    [InlineData(2027, 2, 1, 0, 0, 2026, false)]
    [InlineData(2027, 1, 1, 0, 0, 2027, false)]
    public void CalendarPolicyKeepsTheOriginalLocalDateAndLastYearConditions(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int lastYear,
        bool expected
    )
    {
        var clock = new Clock(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero));
        var writes = 0;
        var policy = new NewYearCelebrationPolicy(
            clock,
            () => lastYear,
            value =>
            {
                lastYear = value;
                writes++;
            }
        );
        Assert.Equal(expected, policy.TryGetCelebrationYear(out var eligibleYear));
        if (expected)
        {
            policy.RememberCelebration(eligibleYear);
            Assert.Equal(year, lastYear);
            Assert.False(policy.TryGetCelebrationYear(out _));
            Assert.Equal(1, writes);
        }
        else { Assert.Equal(0, writes); }
    }

    [Fact]
    public void HostUsesTheCalendarTickAndStartsOnlyForTheFirstAwakeRoleAtNight()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var characters = new SubsystemCharacters();
        var sky = new SubsystemSky();
        var fireworks = new SubsystemFireworksBlockBehavior();
        project.AddSubsystem(characters);
        project.AddSubsystem(sky);
        project.AddSubsystem(fireworks);
        Set(fireworks, "_subsystemCharacters", characters);
        Set(fireworks, "_subsystemSky", sky);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(data, project);
        data.CharacterId = Guid.NewGuid();
        var role = new ComponentCharacter { CharacterData = data, ComponentSleep = new ComponentSleep() };
        ((List<ComponentCharacter>)Get(characters, "_componentCharacters")!).Add(role);
        var lastYear = 2026;
        var host = new SubsystemNewYearCelebrationHosting();
        project.InitializeSubsystem(host);
        host.Configure(
            new NewYearCelebrationPolicy(
                new Clock(new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.Zero)),
                () => lastYear,
                value => lastYear = value
            )
        );
        host.Load(new ValuesDictionary());
        Assert.False(Tick(host, false));
        sky.SkyLightIntensity = .5f;
        Assert.False(Tick(host, true));
        Assert.Equal(2026, lastYear);
        sky.SkyLightIntensity = 0f;
        typeof(ComponentSleep).GetProperty(nameof(ComponentSleep.SleepStartTime))!.SetValue(
            role.ComponentSleep,
            (double?)1
        );
        Assert.False(Tick(host, true));
        typeof(ComponentSleep).GetProperty(nameof(ComponentSleep.SleepStartTime))!.SetValue(role.ComponentSleep, null);
        Assert.True(Tick(host, true));
        Assert.Equal(180f, (float)Get(fireworks, "_newYearCelebrationTimeRemaining")!);
        Assert.Equal(2027, lastYear);
        Assert.False(Tick(host, true));
        host.Dispose();
        Assert.False(Tick(host, true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnnouncementTargetsOnlyTheActualLocalRoleAndKeepsLargeMessageTiming(bool local)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var characters = new SubsystemCharacters();
        project.AddSubsystem(characters);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        data.CharacterId = Guid.NewGuid();
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, local);
        var character = new ComponentCharacter { CharacterData = data };
        project.InitializeComponent(character);
        data.ComponentCharacter = character;
        var gui = new ComponentGui();
        var definition = new DatabaseObject(project.GameDatabase.MemberComponentTemplateType, "Gui")
            { NestingParent = character.Entity.ValuesDictionary.DatabaseObject };
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(definition);
        gui.Initialize(character.Entity, values);
        ((List<Component>)Get(character.Entity, "_components")!).Add(gui);
        var roster = (List<CharacterData>)Get(characters, "_characters")!;
        roster.Add(data);
        try
        {
            var package = new NewYearCelebrationAnnouncementPackage { CharacterId = data.CharacterId };
            var decoded = Assert.IsType<NewYearCelebrationAnnouncementPackage>(
                ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package))
            );
            var before = Time.RealTime;
            var node = WorldNetworkBinding.For(project).RequireNode();
            new NewYearCelebrationAnnouncementPackageHandler().Handle(
                decoded,
                new PackageReceiveContext(node, node.Runtime, null)
            );
            if (local)
            {
                var message = Assert.IsType<ComponentGui.Message>(Get(gui, "_message"));
                Assert.Equal("Happy New Year!", message.LargeText);
                Assert.Equal("--- Enjoy the fireworks ---", message.SmallText);
                Assert.Equal(15f, message.Duration);
                Assert.InRange(message.StartTime, before + 3, Time.RealTime + 3);
            }
            else { Assert.Null(Get(gui, "_message")); }
        }
        finally { roster.Clear(); }
    }

    [Fact]
    public void AnnouncementRejectsAuthorityDirectionAndClosedClientWorld()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var package = new NewYearCelebrationAnnouncementPackage { CharacterId = Guid.NewGuid() };
        var handler = new NewYearCelebrationAnnouncementPackageHandler();
        handler.Handle(package, new PackageReceiveContext(null, WorldRuntimeContext.For(authority), null));
        var runtime = WorldRuntimeContext.For(client);
        runtime.Close();
        handler.Handle(package, new PackageReceiveContext(null, runtime, null));
    }

    [Fact]
    public void HostingLoadRequiresTheActualClockAndPreferenceConfiguration()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var host = new SubsystemNewYearCelebrationHosting();
        project.InitializeSubsystem(host);
        var error = Assert.Throws<InvalidOperationException>(() => host.Load(new ValuesDictionary()));
        Assert.Contains("not configured", error.Message);
    }

    [Fact]
    public void TypedPolicyBindsTheProvidedSettingsInstanceAndHostingHasNoApplicationReference()
    {
        var settings = new HostPreferences { NewYearCelebrationLastYear = 2026 };
        var host = new SubsystemNewYearCelebrationHosting();
        host.Configure(
            new NewYearCelebrationPolicy(
                TimeProvider.System,
                () => settings.NewYearCelebrationLastYear,
                year => settings.NewYearCelebrationLastYear = year
            )
        );
        var policy = Assert.IsType<NewYearCelebrationPolicy>(Get(host, "_policy"));
        policy.RememberCelebration(2042);
        Assert.Equal(2042, settings.NewYearCelebrationLastYear);
        Assert.Equal("Survivalcraft.Hosting", typeof(SubsystemNewYearCelebrationHosting).Assembly.GetName().Name);
        Assert.DoesNotContain(
            typeof(SubsystemNewYearCelebrationHosting).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "Survivalcraft" or "Survivalcraft.Client"
        );
    }

    private static bool Tick(SubsystemNewYearCelebrationHosting host, bool calendarTick)
    {
        return (bool)typeof(SubsystemNewYearCelebrationHosting)
            .GetMethod("TryStartAtCalendarTick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(host, [calendarTick])!;
    }

    private static object? Get(object owner, string field)
    {
        return owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
    }

    private static void Set(object owner, string field, object value)
    {
        owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
