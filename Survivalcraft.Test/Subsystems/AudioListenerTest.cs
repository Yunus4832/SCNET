using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Cameras;
using Game.Characters;
using Game.Network;
using Game.Subsystems;
using Game.Widgets;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class AudioListenerTest
{
    [Theory]
    [InlineData(NodeRole.Authority)]
    [InlineData(NodeRole.Client)]
    public void RemotePlayerViewDoesNotBecomeLocalAudioListener(NodeRole mode)
    {
        var previous = CommonLib.Net.Role;
        CommonLib.Net.Role = mode;
        try
        {
            var localPosition = new Vector3(1000, 70, 1000);
            var views = new SubsystemGameWidgets();
            var widgets = (List<GameWidget>)typeof(SubsystemGameWidgets)
                .GetField("_gameWidgets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(views)!;
            widgets.Add(CreateView(true, localPosition));
            widgets.Add(CreateView(false, Vector3.Zero));
            using var project = new RuntimeTestProject(RuntimeSide.Client);
            var audio = new SubsystemAudio();
            project.InitializeSubsystem(audio);
            typeof(SubsystemAudio).GetField("_subsystemViews", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(audio, views);
            typeof(SubsystemAudio).GetField("_subsystemTime", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(audio, new SubsystemTime());

            audio.Update(0.1f);

            Assert.Equal(localPosition, Assert.Single(audio.ListenerPositions));
            Assert.True(audio.CalculateListenerDistance(Vector3.Zero) > 1000f);
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void AuthorityAudioNeedsNoPresentationSubsystemsOrAssets()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var audio = new SubsystemAudio();
        project.InitializeSubsystem(audio);

        audio.Load(new ValuesDictionary());
        audio.Update(0.1f);
        audio.PlaySound("MissingAuthoritySound", 1f, 0f, 0f, 0f);
        audio.PlayRandomSound("MissingAuthorityDirectory", 1f, 0f, 0f, 0f);

        Assert.Empty(audio.ListenerPositions);
        Assert.Throws<InvalidOperationException>(() => audio.CreateSound("MissingAuthoritySound"));
    }

    private static GameWidget CreateView(bool local, Vector3 position)
    {
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(player, local);
        var widget = (GameWidget)RuntimeHelpers.GetUninitializedObject(typeof(GameWidget));
        var presentation = (CharacterPresentation)RuntimeHelpers.GetUninitializedObject(typeof(CharacterPresentation));
        typeof(CharacterPresentation).GetField(
                "<CharacterData>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .SetValue(presentation, player);
        typeof(GameWidget).GetField("<Presentation>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(widget, presentation);
        var camera = new FppCamera(widget);
        typeof(BasePerspectiveCamera).GetField("_viewPosition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(camera, position);
        typeof(GameWidget).GetField("_activeCamera", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(widget, camera);
        return widget;
    }
}
