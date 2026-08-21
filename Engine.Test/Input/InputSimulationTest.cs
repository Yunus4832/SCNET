using System.Reflection;

using Engine.Core;
using Engine.Input;
using Engine.Windowing;

namespace Engine.Test.Input;

[Collection(nameof(InputSimulationCollection))]
public sealed class InputSimulationTest : IDisposable
{
    private readonly FieldInfo _windowState = typeof(Window).GetField(
        "_state",
        BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly object _previousWindowState;

    public InputSimulationTest()
    {
        _previousWindowState = _windowState.GetValue(null)!;
        _windowState.SetValue(null, Enum.Parse(_windowState.FieldType, "Active"));
        Keyboard.Clear();
        Mouse.Clear();
        Touch.Clear();
        InputSimulation.Clear();
    }

    [Fact]
    public void SimulatedKeyReleaseDoesNotReleasePhysicalKey()
    {
        ProcessPhysicalKey(Key.A, isDown: true);
        InputSimulation.EnqueueKeyDown(Key.A);
        InputSimulation.EnqueueKeyUp(Key.A);

        InputSimulation.BeforeFrame();

        Assert.True(Keyboard.IsKeyDown(Key.A));

        ProcessPhysicalKey(Key.A, isDown: false);

        Assert.False(Keyboard.IsKeyDown(Key.A));
    }

    [Fact]
    public void PhysicalKeyReleaseDoesNotReleaseSimulatedKey()
    {
        InputSimulation.EnqueueKeyDown(Key.B);
        InputSimulation.BeforeFrame();
        ProcessPhysicalKey(Key.B, isDown: true);
        ProcessPhysicalKey(Key.B, isDown: false);

        Assert.True(Keyboard.IsKeyDown(Key.B));

        InputSimulation.EnqueueKeyUp(Key.B);
        InputSimulation.BeforeFrame();

        Assert.False(Keyboard.IsKeyDown(Key.B));
    }

    [Fact]
    public void SimulatedMouseReleaseDoesNotReleasePhysicalButton()
    {
        var position = new Point2(32, 48);
        ProcessPhysicalMouse(MouseButton.Left, position, isDown: true);
        InputSimulation.EnqueueMouseDown(MouseButton.Left, position);
        InputSimulation.EnqueueMouseUp(MouseButton.Left, position);

        InputSimulation.BeforeFrame();

        Assert.True(Mouse.IsMouseButtonDown(MouseButton.Left));

        ProcessPhysicalMouse(MouseButton.Left, position, isDown: false);

        Assert.False(Mouse.IsMouseButtonDown(MouseButton.Left));
    }

    [Fact]
    public void ClearDiscardsQueuedSimulatedEvents()
    {
        InputSimulation.EnqueueKeyDown(Key.C);
        InputSimulation.Clear();

        InputSimulation.BeforeFrame();

        Assert.False(Keyboard.IsKeyDown(Key.C));
    }

    [Fact]
    public void SimulatedTouchCompletesTheSameLifecycleAsPhysicalTouch()
    {
        var id = InputSimulation.AllocateTouchId();
        var pressedPosition = new Vector2(24f, 36f);
        var movedPosition = new Vector2(48f, 72f);

        Assert.True(id < 0);

        InputSimulation.EnqueueTouchPressed(id, pressedPosition);
        InputSimulation.BeforeFrame();

        Assert.Single(Touch.TouchLocations);
        Assert.Equal(id, Touch.TouchLocations[0].Id);
        Assert.Equal(pressedPosition, Touch.TouchLocations[0].Position);
        Assert.Equal(TouchLocationState.Pressed, Touch.TouchLocations[0].State);

        Touch.AfterFrame();
        InputSimulation.EnqueueTouchMoved(id, movedPosition);
        InputSimulation.BeforeFrame();

        Assert.Equal(movedPosition, Touch.TouchLocations[0].Position);
        Assert.Equal(TouchLocationState.Moved, Touch.TouchLocations[0].State);

        InputSimulation.EnqueueTouchReleased(id, movedPosition);
        InputSimulation.BeforeFrame();

        Assert.Equal(TouchLocationState.Released, Touch.TouchLocations[0].State);

        Touch.AfterFrame();

        Assert.Empty(Touch.TouchLocations);
    }

    public void Dispose()
    {
        Keyboard.Clear();
        Mouse.Clear();
        Touch.Clear();
        InputSimulation.Clear();
        _windowState.SetValue(null, _previousWindowState);
    }

    private static void ProcessPhysicalKey(Key key, bool isDown)
    {
        var method = typeof(Keyboard).GetMethod(
            isDown ? "ProcessKeyDown" : "ProcessKeyUp",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        method.Invoke(null, [key, false]);
    }

    private static void ProcessPhysicalMouse(MouseButton button, Point2 position, bool isDown)
    {
        var method = typeof(Mouse).GetMethod(
            isDown ? "ProcessMouseDown" : "ProcessMouseUp",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        method.Invoke(null, [button, position, false]);
    }
}

[CollectionDefinition(nameof(InputSimulationCollection), DisableParallelization = true)]
public sealed class InputSimulationCollection;
