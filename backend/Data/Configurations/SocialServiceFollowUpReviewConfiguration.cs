using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;
namespace TecNM.Api.Data.Configurations;

public sealed class SocialServiceFollowUpReviewConfiguration : AuditableEntityConfiguration<SocialServiceFollowUpReview>
{
    public override void Configure(EntityTypeBuilder<SocialServiceFollowUpReview> b)
    {
        base.Configure(b); b.ToTable("social_service_follow_up_reviews");
        b.Property(x => x.SocialServiceId).HasColumnName("social_service_id"); b.Property(x => x.PeriodId).HasColumnName("period_id");
        b.Property(x => x.Assessment).HasColumnName("assessment").HasMaxLength(20); b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        b.HasOne<SocialService>().WithMany().HasForeignKey(x => x.SocialServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SocialServiceId, x.PeriodId }).IsUnique();
    }
    protected override string GetTableName() => "social_service_follow_up_reviews";
}