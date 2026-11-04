using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public class ExpedientDocumentConfiguration : IEntityTypeConfiguration<ExpedientDocument>
{
    public void Configure(EntityTypeBuilder<ExpedientDocument> builder)
    {
        builder.ToTable("expedient_documents");
        builder.Property(d => d.Version).HasColumnName("xmin").IsRowVersion();
        builder.Property(d => d.DocumentDate).HasColumnName("document_date");
        builder.Property(d => d.Origin).HasColumnName("origin").HasMaxLength(16).HasDefaultValue("uploaded");
        builder.Property(d => d.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64);
        builder.Property(d => d.TemplateVersion).HasColumnName("template_version").HasMaxLength(100);
        builder.HasIndex(d => new { d.StudentId, d.TramiteType, d.TramiteId, d.DocumentCode })
            .IsUnique().AreNullsDistinct(false)
            .HasFilter("deleted_at IS NULL AND status <> 'superseded'")
            .HasDatabaseName("uq_expedient_documents__current");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(d => d.TramiteType).HasColumnName("tramite_type").HasMaxLength(32).IsRequired();
        builder.Property(d => d.TramiteId).HasColumnName("tramite_id");
        builder.Property(d => d.DocumentCode).HasColumnName("document_code").HasMaxLength(64).IsRequired();
        builder.Property(d => d.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(d => d.StoragePath).HasColumnName("storage_path").IsRequired();
        builder.Property(d => d.FileMimeType).HasColumnName("file_mime_type").HasMaxLength(100).IsRequired();
        builder.Property(d => d.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(d => d.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(d => d.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(d => d.ReviewedAt).HasColumnName("reviewed_at");

        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.DeletedBy).HasColumnName("deleted_by");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(d => d.Student)
               .WithMany()
               .HasForeignKey(d => d.StudentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Reviewer)
               .WithMany()
               .HasForeignKey(d => d.ReviewedBy)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(d => d.DeletedAt == null);
    }
}

