using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Rendering;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class SignPresentationOwnershipTest
{
    [Fact]
    public void SharedFactsCopyInputAndOutputAndOwnNoRenderingState()
    {
        var signs = new SubsystemSignBlockBehavior();
        SetInfo(signs, new SubsystemGameInfo { WorldSettings = new WorldSettings() });
        var point = new Point3(1, 64, 1);
        string[] lines = ["original", "", "", ""];
        Color[] colors = [Color.White, Color.White, Color.White, Color.White];
        signs.SetSignData(point, lines, colors, "link");
        lines[0] = "changed input";
        colors[0] = Color.Black;
        var copy = signs.GetSignData(point)!;
        Assert.Equal("original", copy.Lines[0]);
        Assert.Equal(Color.White, copy.Colors[0]);
        copy.Lines[0] = "changed output";
        Assert.Equal("original", signs.GetSignData(point)!.Lines[0]);
        Assert.False(typeof(IDrawable).IsAssignableFrom(typeof(SubsystemSignBlockBehavior)));
        foreach (var field in typeof(SubsystemSignBlockBehavior).GetFields(
                     BindingFlags.Instance | BindingFlags.NonPublic
                 ))
        {
            Assert.DoesNotContain("Engine.Graphics", field.FieldType.FullName ?? "");
            Assert.DoesNotContain("TextData", field.FieldType.FullName ?? "");
        }
    }

    [Fact]
    public void CacheUsesFactCopiesAndInvalidatesOnlyChangedSlots()
    {
        using var fixture = new Fixture();
        var point = new Point3(1, 64, 1);
        var neighbor = new Point3(16, 64, 1);
        fixture.SetText(point, "first");
        fixture.SetText(neighbor, "neighbor");
        var previous = fixture.Cache[point]!;
        previous.GetType().GetField("TextureLocation")!.SetValue(previous, 3);
        fixture.TextureSlots.SetValue(previous, 3);
        fixture.SetText(point, "second");
        Assert.NotSame(previous, fixture.Cache[point]);
        Assert.Null(fixture.TextureSlots.GetValue(3));
        var lines = (string[])fixture.Cache[point]!.GetType().GetField("Lines")!.GetValue(fixture.Cache[point])!;
        Assert.Equal("second", lines[0]);
        lines[0] = "view-only mutation";
        Assert.Equal("second", fixture.Signs.GetSignData(point)!.Lines[0]);
        fixture.Signs.ClearChunkState(new Point2(0, 0));
        Assert.False(fixture.Cache.Contains(point));
        Assert.True(fixture.Cache.Contains(neighbor));
        fixture.Signs.ClearChunkState(new Point2(0, 0));
        Assert.Single(fixture.Cache);
    }

    [Fact]
    public void SurfaceProjectionKeepsStoredTextAndDoesNotShareFactArrays()
    {
        using var fixture = new Fixture();
        var point = new Point3(1, 64, 1);
        fixture.SetText(point, "blocked");
        Assert.Equal("*", fixture.Signs.GetSignData(point)!.Lines[0]);
        var projected = (string[])fixture.Cache[point]!.GetType().GetField("Lines")!.GetValue(fixture.Cache[point])!;
        Assert.Equal("blocked", projected[0]);
        var stored = fixture.Signs.GetStoredSignData(point)!;
        stored.Lines[0] = "changed stored copy";
        stored.Colors[0] = Color.Black;
        Assert.Equal("blocked", projected[0]);
        var projectedColors =
            (Color[])fixture.Cache[point]!.GetType().GetField("Colors")!.GetValue(fixture.Cache[point])!;
        Assert.Equal(Color.White, projectedColors[0]);
        projected[0] = "changed projection";
        projectedColors[0] = Color.Black;
        Assert.Equal("blocked", fixture.Signs.GetStoredSignData(point)!.Lines[0]);
        Assert.Equal(Color.White, fixture.Signs.GetStoredSignData(point)!.Colors[0]);
    }

    [Fact]
    public void DisposedPresenterUnsubscribesAndDoesNotRecreateDeviceResources()
    {
        using var fixture = new Fixture();
        fixture.SetText(new Point3(1, 64, 1), "before");
        fixture.Presentation.Dispose();
        fixture.Presentation.Dispose();
        fixture.Presentation.Update(.016f);
        fixture.Presentation.Draw(null!, 50);
        fixture.SetText(new Point3(2, 64, 1), "after");
        Assert.Empty(fixture.Cache);
        Assert.Null(
            typeof(SubsystemSignBlockPresentation).GetField(
                "_renderTarget",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(fixture.Presentation)
        );
    }

    [Fact]
    public void FactRemovalEventsAreIdempotentAndModerationDoesNotMutateStoredText()
    {
        var signs = new SubsystemSignBlockBehavior();
        var info = new SubsystemGameInfo { WorldSettings = new WorldSettings { KeywordBlocking = "blocked" } };
        SetInfo(signs, info);
        var point = new Point3(-1, 64, -1);
        var changed = new List<Point3>();
        signs.SignDataChanged += changed.Add;
        signs.SetSignData(point, ["blocked", "", "", ""], [Color.White, Color.White, Color.White, Color.White], "");
        Assert.Equal("*", signs.GetSignData(point)!.Lines[0]);
        info.WorldSettings.KeywordBlocking = string.Empty;
        Assert.Equal("blocked", signs.GetSignData(point)!.Lines[0]);
        signs.ClearChunkState(new Point2(-1, -1));
        signs.ClearChunkState(new Point2(-1, -1));
        Assert.Equal(new[] { point, point }, changed);
    }

    private static void SetInfo(SubsystemSignBlockBehavior signs, SubsystemGameInfo info) =>
        typeof(SubsystemSignBlockBehavior).GetField(
            "_subsystemGameInfo",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.SetValue(signs, info);

    private sealed class Fixture : IDisposable
    {
        private readonly RuntimeTestProject _project = new(RuntimeSide.Client);

        public Fixture()
        {
            var info = new SubsystemGameInfo { WorldSettings = new WorldSettings { KeywordBlocking = "blocked" } };
            _project.AddSubsystem(info);
            _project.AddSubsystem(Signs);
            SetInfo(Signs, info);
            _project.AddSubsystem(new SubsystemTerrain());
            _project.AddSubsystem(new SubsystemGameWidgets());
            _project.AddSubsystem(Presentation);
            Presentation.Load(new ValuesDictionary());
        }

        public SubsystemSignBlockBehavior Signs { get; } = new();

        public SubsystemSignBlockPresentation Presentation { get; } = new();

        public IDictionary Cache =>
            (IDictionary)typeof(SubsystemSignBlockPresentation).GetField(
                "_textsByPoint",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(Presentation)!;

        public Array TextureSlots => (Array)typeof(SubsystemSignBlockPresentation).GetField(
            "_textureLocations",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(Presentation)!;

        public void SetText(Point3 point, string text) => Signs.SetSignData(
            point,
            [text, "", "", ""],
            [Color.White, Color.White, Color.White, Color.White],
            ""
        );

        public void Dispose() => _project.Dispose();
    }
}
