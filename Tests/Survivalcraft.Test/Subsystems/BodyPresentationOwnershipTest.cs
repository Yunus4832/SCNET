using System.Reflection;

using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class BodyPresentationOwnershipTest
{
    [Fact]
    public void AuthorityPresentationLoadsWithoutAudioParticlesOrBody()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var presentation = new ComponentBodyPresentation();
        project.InitializeComponent(presentation);
        presentation.Load(new ValuesDictionary(), null!);
        presentation.OnEntityAdded();
        Assert.Null(
            typeof(ComponentBodyPresentation).GetField("_body", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(presentation)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedOrDisposedPresentationUnsubscribesFromBody(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var body = new ComponentBody();
        var presentation = new ComponentBodyPresentation();
        project.InitializeComponent(presentation);
        typeof(ComponentBodyPresentation).GetField("_body", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(presentation, body);
        presentation.OnEntityAdded();
        presentation.OnEntityAdded();
        Assert.Single(GetSubscription(body)!.GetInvocationList());
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        Assert.Null(GetSubscription(body));
    }

    private static Delegate? GetSubscription(ComponentBody body)
    {
        return (Delegate?)typeof(ComponentBody).GetField(
                "ImmersionUpdated",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(body);
    }
}
