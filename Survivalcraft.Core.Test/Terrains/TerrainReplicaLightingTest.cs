using Engine.Core.Mathematics;

using Game.Protocol.Serialization;
using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Lighting;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainReplicaLightingTest
{
    [Fact]
    public void ExpiredAppearanceCannotBePinnedAgainBeforeItsDelayedLightPackageArrives()
    {
        using var terrain = new Terrain();
        var a = Prepare(terrain, 1);
        var b = Prepare(terrain, 1, 1);
        var first = new ChunkAllocationId(a.Coords, a.AllocationGeneration);
        var second = new ChunkAllocationId(b.Coords, b.AllocationGeneration);
        var clock = new PresentationClock();
        var inbox = new TerrainReplicaLighting(terrain, _ => true, clock);
        var dark = new TerrainLightSnapshot(default);
        var lit = dark.WithSection(4, new TerrainLightSection(new TerrainLightChannels(0, 15)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(first, 1, 1, 0, dark, null)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(second, 1, 1, 0, dark, null)));
        Assert.Equal(2, inbox.Drain().Count);
        Assert.True(
            inbox.Receive(
                new TerrainLightBatchPackage
                {
                    Updates =
                    [
                        TerrainLightSectionsCodec.Capture(first, 1, 2, 1, lit, dark),
                        TerrainLightSectionsCodec.Capture(second, 1, 2, 1, lit, dark)
                    ]
                }
            )
        );
        Assert.Equal(2, inbox.Drain().Count);
        var batch = Assert.IsType<TerrainLightPresentationBatch>(a.LightingPresentationBatch);
        var original = inbox.CapturePresentationSources(batch);
        var groups = new Dictionary<TerrainLightPresentationBatch, TerrainLightPresentationBatch.Source[]>
        {
            [batch] = original
        };
        Assert.True(inbox.TryHoldPresentation(groups));
        Assert.True(inbox.HasHeldAppearance(a));

        clock.Advance(TimeSpan.FromMilliseconds(501));
        Assert.False(inbox.TryHoldPresentation(groups));
        Assert.False(inbox.HasHeldAppearance(a));
        Assert.True(inbox.CanPresent(batch, original));

        Assert.True(
            inbox.Receive(
                new TerrainLightBatchPackage
                {
                    Updates =
                    [
                        TerrainLightSectionsCodec.Capture(first, 1, 3, 2, dark, lit),
                        TerrainLightSectionsCodec.Capture(second, 1, 3, 2, dark, lit)
                    ]
                }
            )
        );
        Assert.Equal(2, inbox.Drain().Count);
        groups[batch] = inbox.CapturePresentationSources(batch);
        Assert.True(inbox.TryHoldPresentation(groups));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeldBatchKeepsItsInstalledSourcesAndRetainsTheNewestPendingUpdate(bool expires)
    {
        using var terrain = new Terrain();
        var a = Prepare(terrain, 1);
        var b = Prepare(terrain, 1, 1);
        var first = new ChunkAllocationId(a.Coords, a.AllocationGeneration);
        var second = new ChunkAllocationId(b.Coords, b.AllocationGeneration);
        var clock = new PresentationClock();
        var inbox = new TerrainReplicaLighting(terrain, _ => true, clock);
        var dark = new TerrainLightSnapshot(default);
        var lit = dark.WithSection(4, new TerrainLightSection(new TerrainLightChannels(0, 15)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(first, 1, 1, 0, dark, null)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(second, 1, 1, 0, dark, null)));
        Assert.Equal(2, inbox.Drain().Count);
        Assert.True(
            inbox.Receive(
                new TerrainLightBatchPackage
                {
                    Updates =
                    [
                        TerrainLightSectionsCodec.Capture(first, 1, 2, 1, lit, dark),
                        TerrainLightSectionsCodec.Capture(second, 1, 2, 1, lit, dark)
                    ]
                }
            )
        );
        Assert.Equal(2, inbox.Drain().Count);
        var batch = Assert.IsType<TerrainLightPresentationBatch>(a.LightingPresentationBatch);
        var original = inbox.CapturePresentationSources(batch);
        var groups = new Dictionary<TerrainLightPresentationBatch, TerrainLightPresentationBatch.Source[]>
        {
            [batch] = original
        };
        Assert.True(inbox.TryHoldPresentation(groups));
        Assert.True(inbox.HasHeldAppearance(a));

        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(first, 1, 3, 2, dark, lit)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(first, 1, 4, 3, lit, dark)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(first, 1, 5, 4, dark, lit)));
        Assert.Empty(inbox.Drain());
        Assert.True(inbox.CanPresent(batch, original));
        Assert.Equal(original, inbox.CapturePresentationSources(batch));
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(a, out var published));
        Assert.Equal(15, published.Get(0, 64, 0).Block);

        if (expires)
        {
            clock.Advance(TimeSpan.FromMilliseconds(501));
            Assert.False(inbox.TryHoldPresentation(groups));
        }
        else { inbox.CompletePresentation(groups); }

        Assert.False(inbox.HasHeldAppearance(a));
        Assert.True(batch.IsPending);
        Assert.Single(inbox.Drain());
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(a, out published));
        Assert.Equal(0, published.Get(0, 64, 0).Block);
        groups[batch] = inbox.CapturePresentationSources(batch);
        Assert.True(inbox.CanPresent(batch, groups[batch]));
        inbox.CompletePresentation(groups);
        Assert.False(batch.IsPending);
    }

    private sealed class PresentationClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }

    [Fact]
    public void LightBoundaryMasksDistinguishAnInteriorCellFromACorner()
    {
        var dark = new TerrainLightSnapshot(default);
        var cells = new byte[TerrainLightSection.CellCount];
        cells[(8 << 4) + (8 << 8)] = 15;
        var interior = dark.WithSection(4, TerrainLightSection.Capture(cells));
        var changes = new TerrainLightBoundaryChanges();
        changes.Include(dark, interior, 1 << 4);
        var interiorOnly = changes;
        Assert.Equal(0, changes.ForSampledNeighbor(new Point2(1, 0)));
        Assert.Equal(0, changes.ForSampledNeighbor(new Point2(1, 1)));

        cells[0] = 15;
        var corner = interior.WithSection(4, TerrainLightSection.Capture(cells));
        changes.Include(interior, corner, 1 << 4);
        Assert.Equal(1 << 4, changes.ForSampledNeighbor(new Point2(1, 0)));
        Assert.Equal(1 << 4, changes.ForSampledNeighbor(new Point2(0, 1)));
        Assert.Equal(1 << 4, changes.ForSampledNeighbor(new Point2(1, 1)));
        Assert.Equal(0, changes.ForSampledNeighbor(new Point2(-1, 0)));
        Assert.NotEqual(interiorOnly, changes);
        var allocation = new ChunkAllocationId(Point2.Zero, 1);
        var first = new TerrainLightPresentationBatch.Source(allocation, 2, 1 << 4, changes);
        var copy = first with { Boundaries = changes };
        TerrainLightPresentationBatch.Source[] original = [first];
        TerrainLightPresentationBatch.Source[] repeated = [copy];
        Assert.True(original.AsSpan().SequenceEqual(repeated));
        Assert.Equal(first.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void RealCodecRoundTripInstallsBaselineAndExplicitDarkSectionWithoutChangingCells()
    {
        using var terrain = new Terrain();
        var chunk = Prepare(terrain, 7);
        var allocation = new ChunkAllocationId(chunk.Coords, 8);
        var inbox = new TerrainReplicaLighting(terrain, value => value == allocation);
        var original = new TerrainLightSnapshot(new TerrainLightChannels(15, 4));
        var baseline = TerrainLightSectionsCodec.Capture(allocation, 7, 1, 0, original, null);
        var decoded =
            Assert.IsType<TerrainLightSectionsPackage>(
                ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(baseline))
            );
        Assert.True(inbox.Receive(decoded));
        Assert.Single(inbox.Drain());
        Assert.True(terrain.Lighting.TryGetCurrentSnapshot(chunk, out var installed));
        Assert.True(original.HasSameValues(installed!));
        Assert.Equal(0, chunk.GetCellValueFast(0, 64, 0));

        var dark = original.WithSection(4, new TerrainLightSection(default));
        var delta = TerrainLightSectionsCodec.Capture(allocation, 7, 2, 1, dark, original);
        Assert.False(delta.IsBaseline);
        Assert.Equal(1 << 4, delta.SectionMask);
        Assert.Equal(new byte[] { 0 }, Assert.Single(delta.Sections));
        Assert.True(inbox.Receive(delta));
        Assert.Single(inbox.Drain());
        Assert.True(terrain.Lighting.TryGetCurrentSnapshot(chunk, out var updated));
        Assert.Equal(default, updated!.Get(0, 64, 0));
        Assert.Equal(new TerrainLightChannels(15, 4), updated.Get(0, 32, 0));
    }

    [Fact]
    public void LightBeforeContentsWaitsAndMultipleDeltasRetainAllChangedSections()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var allocation = new ChunkAllocationId(chunk.Coords, 1);
        var inbox = new TerrainReplicaLighting(terrain, value => value == allocation);
        var first = new TerrainLightSnapshot(default);
        var second = first.WithSection(4, new TerrainLightSection(new TerrainLightChannels(0, 15)));
        var third = second.WithSection(5, new TerrainLightSection(new TerrainLightChannels(0, 12)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(allocation, 9, 1, 0, first, null)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(allocation, 9, 2, 1, second, first)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(allocation, 9, 3, 2, third, second)));
        Assert.Empty(inbox.Drain());
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 8;
        chunk.DerivedRevisions.PublishHeights(chunk.DerivedRevisions.Capture());
        Assert.Empty(inbox.Drain());
        chunk.NetworkContentVersion = 9;
        Assert.Single(inbox.Drain());
        Assert.True(terrain.Lighting.TryGetCurrentSnapshot(chunk, out var result));
        Assert.True(third.HasSameValues(result!));
    }

    [Fact]
    public void ANewAllocationNeedsItsOwnBaselineAndRejectsOldPackets()
    {
        using var terrain = new Terrain();
        var chunk = Prepare(terrain, 1);
        var current = new ChunkAllocationId(chunk.Coords, 1);
        var inbox = new TerrainReplicaLighting(terrain, value => value == current);
        var snapshot = new TerrainLightSnapshot(new TerrainLightChannels(0, 15));
        var old = TerrainLightSectionsCodec.Capture(current, 1, 50, 0, snapshot, null);
        Assert.True(inbox.Receive(old));
        inbox.Discard(current);
        current = current with { Generation = 2 };
        Assert.False(inbox.Receive(old));
        Assert.Empty(inbox.Drain());
        var delta = TerrainLightSectionsCodec.Capture(current, 1, 2, 1, snapshot, snapshot);
        Assert.False(inbox.Receive(delta));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(current, 1, 1, 0, snapshot, null)));
        Assert.Single(inbox.Drain());
    }

    [Fact]
    public void SameVersionContentRefreshReinstallsCachedAuthorityLightAfterHeights()
    {
        using var terrain = new Terrain();
        var chunk = Prepare(terrain, 3);
        var allocation = new ChunkAllocationId(chunk.Coords, 1);
        var inbox = new TerrainReplicaLighting(terrain, value => value == allocation);
        var snapshot = new TerrainLightSnapshot(new TerrainLightChannels(0, 14));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(allocation, 3, 1, 0, snapshot, null)));
        Assert.Single(inbox.Drain());
        chunk.DerivedRevisions.InvalidateHeightsAndLight();
        inbox.Requeue(chunk);
        Assert.Empty(inbox.Drain());
        Assert.True(chunk.DerivedRevisions.PublishHeights(chunk.DerivedRevisions.Capture()));
        Assert.Single(inbox.Drain());
        Assert.True(terrain.Lighting.TryGetCurrentSnapshot(chunk, out var restored));
        Assert.True(snapshot.HasSameValues(restored!));
    }

    [Fact]
    public void MissingSequenceAndMalformedSectionsCannotReplaceAcceptedData()
    {
        using var terrain = new Terrain();
        var chunk = Prepare(terrain, 1);
        var allocation = new ChunkAllocationId(chunk.Coords, 1);
        var inbox = new TerrainReplicaLighting(terrain, value => value == allocation);
        var light = new TerrainLightSnapshot(new TerrainLightChannels(0, 15));
        var baseline = TerrainLightSectionsCodec.Capture(allocation, 1, 1, 0, light, null);
        Assert.True(inbox.Receive(baseline));
        Assert.False(inbox.Receive(TerrainLightSectionsCodec.Capture(allocation, 1, 3, 2, light, light)));
        Assert.Throws<InvalidDataException>(() => inbox.Receive(baseline with { Sections = [new byte[2]] }));
        Assert.Single(inbox.Drain());
        Assert.False(inbox.Receive(baseline));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    public void ChunkBatchWaitsForMatchingContentAndKeepsItsMembersWhenSuperseded(int count)
    {
        using var terrain = new Terrain();
        var chunks = Enumerable.Range(0, count).Select(index => Prepare(terrain, 7, index % 3, index / 3)).ToArray();
        var allocations = chunks.Select(chunk => new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration))
            .ToArray();
        var inbox = new TerrainReplicaLighting(terrain, allocations.Contains);
        var dark = new TerrainLightSnapshot(default);
        var lit = dark.WithSection(4, new TerrainLightSection(new TerrainLightChannels(0, 15)));
        foreach (var allocation in allocations)
        {
            Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(allocation, 7, 1, 0, dark, null)));
        }

        Assert.Equal(count, inbox.Drain().Count);
        var updates = allocations.Select((allocation, index) =>
            TerrainLightSectionsCodec.Capture(allocation, index == 0 ? 8 : 7, 2, 1, lit, dark)
        ).ToArray();
        var package = new TerrainLightBatchPackage { Updates = updates };
        var decoded =
            Assert.IsType<TerrainLightBatchPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package)));
        Assert.True(inbox.Receive(decoded));
        var batch = Assert.IsType<TerrainLightPresentationBatch>(chunks[0].LightingPresentationBatch);
        Assert.All(chunks, chunk => Assert.Same(batch, chunk.LightingPresentationBatch));
        Assert.Equal(count - 1, inbox.Drain().Count);
        Assert.False(inbox.IsPresentationApplied(chunks[0], batch));
        Assert.All(chunks.Skip(1), chunk => Assert.True(inbox.IsPresentationApplied(chunk, batch)));

        var versionOnly = TerrainLightSectionsCodec.Capture(allocations[0], 9, 3, 2, lit, lit);
        Assert.True(inbox.Receive(versionOnly));
        Assert.Same(batch, chunks[0].LightingPresentationBatch);
        Assert.Equal(0, versionOnly.SectionMask);
        chunks[0].NetworkContentVersion = 9;
        Assert.Same(chunks[0], Assert.Single(inbox.Drain()));
        Assert.All(chunks, chunk => Assert.True(inbox.IsPresentationApplied(chunk, batch)));

        var sources = inbox.CapturePresentationSources(batch);
        Assert.True(inbox.CanPresent(batch, sources));
        var shifted = dark.WithSection(5, new TerrainLightSection(new TerrainLightChannels(0, 15)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(allocations[^1], 7, 3, 2, shifted, lit)));
        Assert.True(batch.IsPending);
        Assert.False(inbox.CanPresent(batch, sources));
        var next = inbox.CapturePresentationSources(batch);
        Assert.Equal((1 << 4) | (1 << 5), next.Single(source => source.Allocation == allocations[^1]).Sections);
        Assert.False(inbox.CanPresent(batch, next));
        Assert.False(inbox.IsPresentationApplied(chunks[^1], batch));
        Assert.Same(chunks[^1], Assert.Single(inbox.Drain()));
        Assert.True(inbox.CanPresent(batch, next));
        Assert.All(chunks, chunk => Assert.True(inbox.IsPresentationApplied(chunk, batch)));
    }

    [Fact]
    public void OverlappingLightBatchesKeepEarlierMembersAndWaitForTheNewContent()
    {
        using var terrain = new Terrain();
        var chunks = Enumerable.Range(0, 8).Select(index => Prepare(terrain, 1, index, 0)).ToArray();
        var allocations = chunks.Select(chunk => new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration))
            .ToArray();
        var inbox = new TerrainReplicaLighting(terrain, allocations.Contains);
        var dark = new TerrainLightSnapshot(default);
        var lit = dark.WithSection(4, new TerrainLightSection(new TerrainLightChannels(0, 15)));
        foreach (var allocation in allocations)
        {
            Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(allocation, 1, 1, 0, dark, null)));
        }

        Assert.Equal(8, inbox.Drain().Count);
        Assert.True(
            inbox.Receive(
                new TerrainLightBatchPackage
                {
                    Updates = allocations[..7].Select(allocation =>
                        TerrainLightSectionsCodec.Capture(allocation, 1, 2, 1, lit, dark)
                    ).ToArray()
                }
            )
        );
        var previous = Assert.IsType<TerrainLightPresentationBatch>(chunks[0].LightingPresentationBatch);
        Assert.Equal(7, inbox.Drain().Count);
        Assert.True(
            inbox.Receive(
                new TerrainLightBatchPackage
                {
                    Updates =
                    [
                        TerrainLightSectionsCodec.Capture(allocations[6], 2, 3, 2, dark, lit),
                        TerrainLightSectionsCodec.Capture(allocations[7], 1, 2, 1, lit, dark)
                    ]
                }
            )
        );
        var merged = Assert.IsType<TerrainLightPresentationBatch>(chunks[0].LightingPresentationBatch);
        Assert.False(previous.IsPending);
        Assert.True(merged.IsPending);
        Assert.Equal(8, merged.Allocations.Count);
        Assert.All(chunks, chunk => Assert.Same(merged, chunk.LightingPresentationBatch));
        Assert.Same(chunks[7], Assert.Single(inbox.Drain()));
        Assert.False(inbox.IsPresentationApplied(chunks[6], merged));
        Assert.True(inbox.IsPresentationApplied(chunks[0], merged));

        inbox.Discard(allocations[7]);
        Assert.Null(chunks[7].LightingPresentationBatch);
        Assert.True(merged.IsPending);
        chunks[6].NetworkContentVersion = 2;
        Assert.Same(chunks[6], Assert.Single(inbox.Drain()));
        Assert.All(chunks[..7], chunk => Assert.True(inbox.IsPresentationApplied(chunk, merged)));
    }

    [Fact]
    public void StaleAllocationDoesNotReserveValidNeighborsOrDropTheirLight()
    {
        using var terrain = new Terrain();
        var left = Prepare(terrain, 1);
        var right = Prepare(terrain, 1, 1, 0);
        var a = new ChunkAllocationId(left.Coords, left.AllocationGeneration);
        var b = new ChunkAllocationId(right.Coords, right.AllocationGeneration);
        var rightCurrent = true;
        var inbox = new TerrainReplicaLighting(
            terrain,
            allocation => allocation == a || allocation == b && rightCurrent
        );
        var dark = new TerrainLightSnapshot(default);
        var lit = dark.WithSection(4, new TerrainLightSection(new TerrainLightChannels(0, 5)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(a, 1, 1, 0, dark, null)));
        Assert.True(inbox.Receive(TerrainLightSectionsCodec.Capture(b, 1, 1, 0, dark, null)));
        inbox.Drain();
        rightCurrent = false;

        Assert.True(
            inbox.Receive(
                new TerrainLightBatchPackage
                {
                    Updates =
                    [
                        TerrainLightSectionsCodec.Capture(a, 1, 2, 1, lit, dark),
                        TerrainLightSectionsCodec.Capture(b, 1, 2, 1, lit, dark)
                    ]
                }
            )
        );
        Assert.Null(left.LightingPresentationBatch);
        Assert.Same(left, Assert.Single(inbox.Drain()));
        Assert.True(terrain.Lighting.TryGetCurrentSnapshot(left, out var snapshot));
        Assert.Equal(5, snapshot.Get(0, 64, 0).Block);
    }

    private static TerrainChunk Prepare(Terrain terrain, long version, int x = 0, int z = 0)
    {
        var chunk = terrain.AllocateChunk(x, z);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = version;
        Assert.True(chunk.DerivedRevisions.PublishHeights(chunk.DerivedRevisions.Capture()));
        return chunk;
    }
}
