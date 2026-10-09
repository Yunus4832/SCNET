using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Infrastructure;

internal static class SoftDeleteConfiguration
{
    public static void ConfigureSoftDelete<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<Deleted>("Deleted")
            .HasConversion(value => value.Value, value => new Deleted(value))
            .HasDefaultValue(new Deleted(false));
        builder.HasQueryFilter(entity => EF.Property<Deleted>(entity, "Deleted") == new Deleted(false));
    }
}
