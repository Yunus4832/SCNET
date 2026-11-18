namespace Game.Terrains.Lighting;

/// <summary>Section-sized wire data; unchanged immutable sections are retained by both ends.</summary>
public static class TerrainLightSectionsCodec
{
    public static TerrainLightSectionsPackage Capture(
        ChunkAllocationId allocation,
        long contentVersion,
        long sequence,
        long baseSequence,
        TerrainLightSnapshot snapshot,
        TerrainLightSnapshot? previous
    )
    {
        var mask = (ushort)0;
        var values = new List<byte[]>();
        Span<byte> cells = stackalloc byte[TerrainLightSection.CellCount];
        for (var slice = 0; slice < TerrainChunk.SlicesCount; slice++)
        {
            var section = snapshot.GetSection(slice);
            if (previous != null && section.HasSameValues(previous.GetSection(slice))) { continue; }

            mask |= (ushort)(1 << slice);
            section.CopyTo(cells);
            values.Add(cells.IndexOfAnyExcept(cells[0]) < 0 ? [cells[0]] : cells.ToArray());
        }

        return new TerrainLightSectionsPackage
        {
            Allocation = allocation,
            ContentVersion = contentVersion,
            Sequence = sequence,
            BaseSequence = previous == null ? 0 : baseSequence,
            IsBaseline = previous == null,
            SectionMask = mask,
            Sections = values.ToArray()
        };
    }

    public static TerrainLightSnapshot Apply(TerrainLightSectionsPackage package, TerrainLightSnapshot? previous)
    {
        ProtocolMessageValidation.Validate(package);
        if (!package.IsBaseline && previous == null)
        {
            throw new InvalidDataException("A light delta requires its allocation baseline.");
        }

        var result = package.IsBaseline ? new TerrainLightSnapshot(default) : previous!;
        var index = 0;
        for (var slice = 0; slice < TerrainChunk.SlicesCount; slice++)
        {
            if ((package.SectionMask & (1 << slice)) == 0) { continue; }

            var values = package.Sections[index++];
            var section = values.Length == 1
                ? new TerrainLightSection(TerrainLightChannels.FromPacked(values[0]))
                : TerrainLightSection.Capture(values);
            result = result.WithSection(slice, section);
        }

        return result;
    }
}
