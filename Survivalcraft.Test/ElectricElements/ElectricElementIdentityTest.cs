using System.Reflection;

using Game;
using Game.ElectricElements;
using Game.Subsystems;

namespace Survivalcraft.Test.ElectricElements;

public class ElectricElementIdentityTest
{
    [Fact]
    public void SchedulingUsesIdentityWithoutCallingElementHashCode()
    {
        var subsystem = new SubsystemElectricity();
        var first = new ValueEqualElectricElement(subsystem, new CellFace(0, 0, 0, 0));
        var second = new ValueEqualElectricElement(subsystem, new CellFace(0, 0, 0, 0));

        subsystem.QueueElectricElementForSimulation(first, 1);
        subsystem.QueueElectricElementForSimulation(second, 1);
        subsystem.QueueElectricElementForSimulation(first, 1);

        var scheduledElements = GetScheduledElements(subsystem, 1);
        Assert.Equal(2, scheduledElements.Count);
        Assert.Contains(first, scheduledElements.Keys, ReferenceEqualityComparer.Instance);
        Assert.Contains(second, scheduledElements.Keys, ReferenceEqualityComparer.Instance);
        Assert.Equal(0, first.HashCodeCallCount);
        Assert.Equal(0, second.HashCodeCallCount);
    }

    [Fact]
    public void HashingLargeWireDomainDoesNotAllocateManagedMemory()
    {
        var subsystem = new SubsystemElectricity();
        var cellFaces = Enumerable.Range(0, 10_000)
            .Select(index => new CellFace(index, 0, 0, 0));
        var element = new TestElectricElement(subsystem, cellFaces);
        _ = element.GetHashCode();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var hash = 0;
        for (var i = 0; i < 256; i++)
        {
            hash ^= element.GetHashCode();
        }

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(hash);
        Assert.Equal(allocatedBefore, allocatedAfter);
    }

    private static Dictionary<ElectricElement, bool> GetScheduledElements(
        SubsystemElectricity subsystem,
        int circuitStep)
    {
        var field = typeof(SubsystemElectricity).GetField(
            "_futureSimulateLists",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var lists = Assert.IsType<Dictionary<int, Dictionary<ElectricElement, bool>>>(field?.GetValue(subsystem));
        return lists[circuitStep];
    }

    private class TestElectricElement(
        SubsystemElectricity subsystemElectricity,
        IEnumerable<CellFace> cellFaces)
        : ElectricElement(subsystemElectricity, cellFaces);

    private sealed class ValueEqualElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace)
        : TestElectricElement(subsystemElectricity, [cellFace])
    {
        public int HashCodeCallCount { get; private set; }

        public override bool Equals(object? obj)
        {
            return obj is ValueEqualElectricElement;
        }

        public override int GetHashCode()
        {
            HashCodeCallCount++;
            return 0;
        }
    }
}
