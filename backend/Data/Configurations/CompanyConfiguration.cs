using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public sealed class CompanyConfiguration : AuditableEntityConfiguration<Company>
{
    public override void Configure(EntityTypeBuilder<Company> builder)
    {
        base.Configure(builder);

        builder.ToTable("companies");
        builder.HasKey(c => c.Id).HasName("pk_companies");

        builder.Property(c => c.Rfc).HasColumnName("rfc").HasMaxLength(13);
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.CommercialName).HasColumnName("commercial_name").HasMaxLength(200);
        builder.Property(c => c.CompanyType).HasColumnName("company_type").HasMaxLength(16).IsRequired();
        builder.HasIndex(c => c.CompanyType).HasDatabaseName("idx_companies__company_type");
        builder.Property(c => c.Sector).HasColumnName("sector").HasMaxLength(50).HasDefaultValue("private").IsRequired();
        builder.Property(c => c.CompanySize).HasColumnName("company_size").HasMaxLength(32).HasDefaultValue("not_applicable").IsRequired();
        builder.Property(c => c.IsTecnmInternal).HasColumnName("is_tecnm_internal").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.Address).HasColumnName("address");
        builder.Property(c => c.City).HasColumnName("city").HasMaxLength(100).HasDefaultValue("Monclova").IsRequired();
        builder.Property(c => c.State).HasColumnName("state").HasMaxLength(100).HasDefaultValue("Coahuila").IsRequired();
        builder.Property(c => c.PostalCode).HasColumnName("postal_code").HasMaxLength(10);
        builder.Property(c => c.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.HasIndex(c => c.Name).HasDatabaseName("idx_companies__name");
        builder.HasIndex(c => c.Sector).HasDatabaseName("idx_companies__sector");
        builder.HasIndex(c => c.CompanySize).HasDatabaseName("idx_companies__company_size");
        builder.HasIndex(c => c.IsTecnmInternal).HasDatabaseName("idx_companies__is_tecnm_internal");

        builder.HasMany(c => c.Supervisors)
            .WithOne(s => s.Company)
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_supervisors__companies");

        builder.HasMany(c => c.Agreements)
            .WithOne(a => a.Company)
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_agreements__companies");
    }

    protected override string GetTableName() => "companies";
}
