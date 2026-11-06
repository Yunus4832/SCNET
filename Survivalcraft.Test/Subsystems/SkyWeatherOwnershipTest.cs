using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Handling;
using Game.Network.Transport;
using Game.Rendering;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

using GameRandom = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

public sealed class SkyWeatherOwnershipTest
{
    [Theory]
    [InlineData(typeof(SubsystemGlow))]
    [InlineData(typeof(SubsystemModelsRenderer))]
    [InlineData(typeof(SubsystemParticles))]
    public void AuthorityViewServicesDoNotResolveClientSkyDuringLoading(Type subsystemType)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var subsystem = (Subsystem)Activator.CreateInstance(subsystemType)!;
        project.InitializeSubsystem(subsystem);
        subsystem.Load(new ValuesDictionary());
    }

    [Fact]
    public void AuthorityCanStrikeRepeatedlyWithoutDrawingAndStillQueuesRealExplosions()
    {
        using var fixture = new SkyFixture(RuntimeSide.Authority);
        var observed = 0;
        fixture.Sky.LightningStrike += _ => observed++;
        fixture.SetTime(2);
        fixture.Sky.MakeLightningStrike(new Vector3(3, 64, 4));
        fixture.SetTime(2.5);
        fixture.Sky.MakeLightningStrike(new Vector3(3, 64, 4));
        fixture.SetTime(3.1);
        fixture.Sky.MakeLightningStrike(new Vector3(3, 64, 4));
        Assert.Equal(2, observed);
        var queued =
            (IList)typeof(SubsystemExplosions).GetField(
                "_queuedExplosions",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(fixture.Explosions)!;
        Assert.Equal(2, queued.Count);
        foreach (var entry in queued)
        {
            var type = entry!.GetType();
            Assert.Equal(3, type.GetField("X")!.GetValue(entry));
            Assert.Equal(65, type.GetField("Y")!.GetValue(entry));
            Assert.Equal(4, type.GetField("Z")!.GetValue(entry));
            Assert.Contains((float)type.GetField("Pressure")!.GetValue(entry)!, new[] { 19f, 39f });
        }
    }

    [Fact]
    public void VisualRandomSamplingDoesNotChangeAuthorityExplosionOutcomes()
    {
        using var control = new SkyFixture(RuntimeSide.Authority);
        using var sampled = new SkyFixture(RuntimeSide.Authority);
        var skyView = new SubsystemSkyPresentation();
        var weatherView = new SubsystemWeatherPresentation();
        ((GameRandom)Get(control.Sky, "_random")!).Seed(123);
        ((GameRandom)Get(sampled.Sky, "_random")!).Seed(123);
        var skyRandom = (GameRandom)Get(skyView, "_random")!;
        var weatherRandom = (GameRandom)Get(weatherView, "_random")!;
        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 1000; j++)
            {
                skyRandom.Float();
                weatherRandom.Float();
            }

            control.SetTime(2 + 2 * i);
            sampled.SetTime(2 + 2 * i);
            control.Sky.MakeLightningStrike(Vector3.Zero);
            sampled.Sky.MakeLightningStrike(Vector3.Zero);
        }

        static float[] Pressures(SubsystemExplosions explosions)
        {
            var queued = (IEnumerable)typeof(SubsystemExplosions).GetField(
                "_queuedExplosions",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(explosions)!;
            return queued.Cast<object>().Select(entry => (float)entry.GetType().GetField("Pressure")!.GetValue(entry)!)
                .ToArray();
        }

        Assert.Equal(Pressures(control.Explosions), Pressures(sampled.Explosions));
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, 0)]
    [InlineData(RuntimeSide.Client, 1)]
    public void NetworkReplayOnlyNotifiesClientsAndNeverExecutesDamage(RuntimeSide side, int expected)
    {
        using var fixture = new SkyFixture(side);
        var observed = 0;
        fixture.Sky.LightningStrike += _ => observed++;
        var node = Game.Network.WorldNetworkBinding.For(fixture.Project).RequireNode();
        new SubsystemSkyPackageHandler().Handle(
            new SubsystemSkyPackage(new Vector3(3, 64, 4)),
            new PackageReceiveContext(node, node.Runtime, null)
        );
        Assert.Equal(expected, observed);
        Assert.Empty(
            (IEnumerable)typeof(SubsystemExplosions).GetField(
                "_queuedExplosions",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(fixture.Explosions)!
        );
        fixture.Sky.ApplyLightningStrike(new Vector3(3, 64, 4));
        Assert.Equal(expected * 2, observed);
    }

    [Fact]
    public void ClientFlashDecaysInUpdateWithoutAViewOrDrawCall()
    {
        using var fixture = new SkyFixture(RuntimeSide.Client);
        var presentation = new SubsystemSkyPresentation();
        fixture.Project.InitializeSubsystem(presentation);
        Set(presentation, "_sky", fixture.Sky);
        Set(presentation, "_subsystemTime", fixture.Time);
        Set(presentation, "_subsystemAudio", new SubsystemAudio());
        var handler = (Action<Vector3>)Delegate.CreateDelegate(
            typeof(Action<Vector3>),
            presentation,
            typeof(SubsystemSkyPresentation).GetMethod(
                "OnLightningStrike",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
        );
        fixture.Sky.LightningStrike += handler;
        fixture.Sky.ApplyLightningStrike(new Vector3(3, 64, 4));
        Assert.NotNull(Get(presentation, "_lightningStrikePosition"));
        typeof(SubsystemTime).GetProperty(nameof(SubsystemTime.GameTimeDelta))!.SetValue(fixture.Time, .1f);
        for (var i = 0; i < 10; i++)
        {
            fixture.SetTime(i * .1);
            presentation.Update(.1f);
        }

        Assert.Null(Get(presentation, "_lightningStrikePosition"));
        Assert.Equal(0f, Get(presentation, "_lightningStrikeBrightness"));
    }

    [Fact]
    public void ClosedWorldDoesNotEmitNewLightningOrQueueExplosions()
    {
        using var fixture = new SkyFixture(RuntimeSide.Authority);
        fixture.SetTime(2);
        WorldRuntimeContext.For(fixture.Project).Close();
        fixture.Sky.MakeLightningStrike(Vector3.Zero);
        Assert.Empty(
            (IEnumerable)typeof(SubsystemExplosions).GetField(
                "_queuedExplosions",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(fixture.Explosions)!
        );
    }

    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void WeatherIntensityAndFogAdvanceWithoutAnyPresentationSubsystem(RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side);
        var weather = new SubsystemWeather
        {
            PrecipitationStartTime = 0, PrecipitationEndTime = 100,
            FogStartTime = 0, FogEndTime = 100, FogRampTime = 25
        };
        project.InitializeSubsystem(weather);
        Set(
            weather,
            "_subsystemGameInfo",
            new SubsystemGameInfo
            {
                TotalElapsedGameTime = 50,
                WorldSettings = new WorldSettings { AreWeatherEffectsEnabled = true }
            }
        );
        Set(weather, "_precipitationRampTime", 25f);
        weather.SubsystemTime = new SubsystemTime();
        weather.Update(.1f);
        Assert.Equal(1f, weather.PrecipitationIntensity);
        Assert.Equal(.004f, weather.FogIntensity, 6);
        Assert.Equal(.001f, weather.FogProgress, 6);
    }

    [Fact]
    public void ClientFreezeRequestReturnsBeforeTouchingAuthorityTerrainOrScanner()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var weather = new SubsystemWeather();
        project.InitializeSubsystem(weather);
        weather.FreezeThawAndDepositSnow(null!, 1f, 1f, true);
    }

    [Theory]
    [InlineData(typeof(SubsystemSky))]
    [InlineData(typeof(SubsystemWeather))]
    public void RuleSubsystemsHaveNoViewDeviceOrParticleFields(Type rule)
    {
        Assert.False(typeof(IDrawable).IsAssignableFrom(rule));
        foreach (var field in rule.GetFields(
                     BindingFlags.Instance |
                     BindingFlags.Static |
                     BindingFlags.Public |
                     BindingFlags.NonPublic |
                     BindingFlags.DeclaredOnly
                 ))
        {
            var name = field.FieldType.FullName ?? string.Empty;
            Assert.DoesNotContain("Engine.Graphics", name);
            Assert.DoesNotContain("Engine.Audio", name);
            Assert.DoesNotContain("Game.Cameras", name);
            Assert.DoesNotContain("Game.ParticleSystems", name);
        }
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static object? Get(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);

    private sealed class SkyFixture : IDisposable
    {
        public SkyFixture(RuntimeSide side)
        {
            Project = new RuntimeTestProject(side);
            Project.AddSubsystem(Sky);
            Project.AddSubsystem(Explosions);
            Set(Sky, "_subsystemTime", Time);
            Set(Sky, "_subsystemBodies", new SubsystemBodies());
            Set(
                Sky,
                "_subsystemGameInfo",
                new SubsystemGameInfo
                    { WorldSettings = new WorldSettings { EnvironmentBehaviorMode = EnvironmentBehaviorMode.Living } }
            );
        }

        public RuntimeTestProject Project { get; }

        public SubsystemSky Sky { get; } = new();

        public SubsystemTime Time { get; } = new();

        public SubsystemExplosions Explosions { get; } = new();

        public void SetTime(double value) =>
            typeof(SubsystemTime).GetProperty(nameof(SubsystemTime.GameTime))!.SetValue(Time, value);

        public void Dispose() => Project.Dispose();
    }
}
