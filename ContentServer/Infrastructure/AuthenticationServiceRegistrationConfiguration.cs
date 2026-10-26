using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using NetCorePal.Extensions.Repository.EntityFrameworkCore;

namespace ContentServer.Infrastructure;

public sealed class
    AuthenticationServiceRegistrationConfiguration : IEntityTypeConfiguration<AuthenticationServiceRegistration>
{
    public void Configure(EntityTypeBuilder<AuthenticationServiceRegistration> builder)
    {
        builder.ConfigureSoftDelete();
        builder.ToTable("AuthenticationServices");
        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.Id).UseGuidVersion7ValueGenerator();
        builder.Property(registration => registration.Status).HasConversion<string>();
        builder.Property(registration => registration.Revision).IsConcurrencyToken();
        builder.HasOne<Publisher>().WithMany().HasForeignKey(registration => registration.PublisherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(registration => registration.PublisherId);
        builder.HasIndex(registration => registration.ServiceUrl).IsUnique().HasFilter("\"Deleted\" = 0");
        builder.HasIndex(registration => registration.Status);
        builder.OwnsMany(
            registration => registration.Audit,
            audit =>
            {
                audit.ToTable("AuthenticationServiceAudit");
                audit.WithOwner().HasForeignKey("RegistrationId");
                audit.HasKey("RegistrationId", nameof(AuthenticationServiceAuditEntry.Revision));
                audit.Property(entry => entry.Revision).ValueGeneratedNever();
                audit.Property(entry => entry.Status).HasConversion<string>();
            }
        );
        builder.Navigation(registration => registration.Audit).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
