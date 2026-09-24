using System.Reflection;
using System.Runtime.CompilerServices;

using Game;
using Game.ElectricElements;
using Game.Subsystems;

namespace Survivalcraft.Test.ElectricElements;

public class RealTimeClockElectricElementTest
{
    private const double _timeOfDayOffsetFix = 0.30000001192092896;

    [Fact]
    public void SchedulingUsesConfiguredDayDuration()
    {
        Assert.Equal(36, GetNextScheduledCircuitStep(1200f));
        Assert.Equal(18, GetNextScheduledCircuitStep(600f));
        Assert.Equal(9, GetNextScheduledCircuitStep(300f));
    }

    private static int GetNextScheduledCircuitStep(float dayDuration)
    {
        const double clockTicks = 100.25;
        var timeOfDay = new SubsystemTimeOfDay
        {
            SubsystemGameInfo = new SubsystemGameInfo
            {
                TotalElapsedGameTime = clockTicks / 4096.0 * dayDuration
            },
            TimeOfDayOffset = -_timeOfDayOffsetFix
        };
        typeof(SubsystemTimeOfDay)
            .GetProperty(nameof(SubsystemTimeOfDay.DayDuration))!
            .SetValue(timeOfDay, dayDuration);

        var electricity = new SubsystemElectricity();
        var clock = (RealTimeClockElectricElement)RuntimeHelpers.GetUninitializedObject(
            typeof(RealTimeClockElectricElement));
        typeof(RealTimeClockElectricElement)
            .GetField("_subsystemTimeOfDay", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(clock, timeOfDay);
        clock.SubsystemElectricity = electricity;

        clock.Simulate();

        var field = typeof(SubsystemElectricity).GetField(
            "_futureSimulateLists",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var lists = Assert.IsType<Dictionary<int, Dictionary<ElectricElement, bool>>>(field?.GetValue(electricity));
        return Assert.Single(lists.Keys);
    }
}
