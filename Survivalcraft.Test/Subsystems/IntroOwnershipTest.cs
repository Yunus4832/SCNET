using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class IntroOwnershipTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, false)]
    [InlineData(RuntimeSide.Client, true)]
    public void PendingIntroIsAcknowledgedOnlyByAuthorityAndPersists(RuntimeSide side, bool expected)
    {
        using var project = new RuntimeTestProject(side);
        var intro = new ComponentIntro();
        project.InitializeComponent(intro);
        var values = new ValuesDictionary();
        values.SetValue("PlayIntro", true);
        intro.Load(values, null!);
        intro.AcknowledgePresentation();
        var saved = new ValuesDictionary();
        intro.Save(saved, null!);
        Assert.Equal(expected, saved.GetValue<bool>("PlayIntro"));
        var restored = new ComponentIntro();
        restored.Load(saved, null!);
        Assert.Equal(expected, restored.PlayIntro);
        Assert.False(intro is IUpdateable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LivePlayingAcknowledgementConsumesOnlyItsOwnersIntroEvenWhenStateWasAlreadyPlaying(bool ownerSender)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var characters = new SubsystemCharacters();
        project.AddSubsystem(characters);
        var actor = new ComponentCharacter();
        project.InitializeComponent(actor);
        var intro = new ComponentIntro();
        intro.Initialize(actor.Entity, actor.ValuesDictionary);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(actor.Entity)!).Add(intro);
        var values = new ValuesDictionary();
        values.SetValue("PlayIntro", true);
        intro.Load(values, null!);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(data, project);
        data.CharacterId = Guid.NewGuid();
        data.ComponentCharacter = actor;
        ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(characters)!).Add(data);
        var runtime = WorldRuntimeContext.For(project);
        var owner = new Client(null, 1, Guid.NewGuid(), data.CharacterId) { State = ClientState.Playing };
        WorldNetworkBinding.For(runtime).RequireNode().AddClient(owner);
        var sender = ownerSender ? owner : new Client(null, 2, Guid.NewGuid(), Guid.Empty);
        new ClientPackageHandler().Handle(
            ClientPackageBuilder.State(owner) with { State = ClientState.Playing },
            new PackageReceiveContext(WorldNetworkBinding.For(runtime).RequireNode(), runtime, sender)
        );
        Assert.Equal(!ownerSender, intro.PlayIntro);
    }

    [Fact]
    public void AuthorityIntroFeedbackDoesNotResolveClientDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        foreach (var view in new Component[] { new ComponentIntroPresentation() })
        {
            project.InitializeComponent(view);
            view.Entity.IsAddedToProject = true;
            view.Load(new ValuesDictionary(), null!);
            Assert.False(SubsystemUpdate.ShouldScheduleUpdateable((IUpdateable)view, WorldRuntimeContext.For(project)));
            ((IUpdateable)view).Update(1f);
            view.OnEntityRemoved();
            view.Dispose();
        }
    }

    [Fact]
    public void IntroMessagesUseSceneRelativeTimeAndCannotSurviveRemovalAndReaddition()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var time = new SubsystemTime();
        project.InitializeSubsystem(time);
        var view = new ComponentIntroPresentation();
        project.InitializeComponent(view);
        typeof(ComponentIntroPresentation).GetField("_subsystemTime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(view, time);
        typeof(ComponentIntroPresentation).GetField("_startTime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(view, 100d);
        var called = false;
        typeof(ComponentIntroPresentation).GetMethod("QueueMessage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(view, [2d, (Action)(() => called = true)]);
        var requests = (IList)typeof(SubsystemTime).GetField(
                "_delayedExecutionsRequests",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(time)!;
        var request = Assert.Single(requests.Cast<object>());
        Assert.Equal(102d, request.GetType().GetField("GameTime")!.GetValue(request));
        var machine = (StateMachine)typeof(ComponentIntroPresentation)
            .GetField("_stateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        machine.AddState("TestActive", () => { }, () => { }, () => { });
        machine.TransitionTo("TestActive");
        view.OnEntityRemoved();
        Assert.Equal(string.Empty, machine.CurrentState);
        view.OnEntityAdded();
        ((Action)request.GetType().GetField("Action")!.GetValue(request)!)();
        Assert.False(called);
    }
}
