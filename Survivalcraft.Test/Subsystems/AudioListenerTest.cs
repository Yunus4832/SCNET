using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Cameras;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;
using Game.Widgets;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class AudioListenerTest
{
    [Theory]
    [InlineData(WorkType.Server)]
    [InlineData(WorkType.Client)]
    public void RemotePlayerViewDoesNotBecomeLocalAudioListener(WorkType mode)
    {
        var previous = CommonLib.WorkType;
        CommonLib.WorkType = mode;
        try
        {
            var localPosition = new Vector3(1000, 70, 1000);
            var views = new SubsystemGameWidgets();
            var widgets = (List<GameWidget>)typeof(SubsystemGameWidgets)
                .GetField("_gameWidgets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(views)!;
            widgets.Add(CreateView(true, localPosition));
            widgets.Add(CreateView(false, Vector3.Zero));
            var audio = new SubsystemAudio();
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
            CommonLib.WorkType = previous;
        }
    }

    private static GameWidget CreateView(bool local, Vector3 position)
    {
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        typeof(PlayerData).GetProperty(nameof(PlayerData.IsMainPlayer))!.SetValue(player, local);
        var widget = (GameWidget)RuntimeHelpers.GetUninitializedObject(typeof(GameWidget));
        widget.PlayerData = player;
        var camera = new FppCamera(widget);
        typeof(BasePerspectiveCamera).GetField("_viewPosition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(camera, position);
        typeof(GameWidget).GetField("_activeCamera", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(widget, camera);
        return widget;
    }
}
