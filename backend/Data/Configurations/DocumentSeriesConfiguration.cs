using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public sealed class DocumentSeriesConfiguration : AuditableEntityConfiguration<DocumentSeries>
{
    public override void Configure(EntityTypeBuilder<DocumentSeries> builder)
    {
        base.Configure(builder);

        builder.Property(s => s.Version).HasColumnName("xmin").IsRowVersion();
        builder.ToTable("document_series");
        builder.HasKey(s => s.Id).HasName("pk_document_series");
        builder.Property(s => s.DocumentType).HasColumnName("document_type").HasMaxLength(64).IsRequired();
        builder.Property(s => s.Prefix).HasColumnName("prefix").HasMaxLength(20).IsRequired();
        builder.Property(s => s.CurrentYear).HasColumnName("current_year").IsRequired();
        builder.Property(s => s.CurrentSequence).HasColumnName("current_sequence").HasDefaultValue(0).IsRequired();
        builder.Property(s => s.FormatPattern).HasColumnName("format_pattern").HasMaxLength(100).HasDefaultValue("{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}").IsRequired();
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(255);

        builder.HasIndex(s => new { s.DocumentType, s.CurrentYear })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_document_series__type_year");
    }

    protected override string GetTableName() => "document_series";
}
