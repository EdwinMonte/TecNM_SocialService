namespace TecNM.Api.Core.Entities;

public sealed class StudentSocialServiceFollowUp
{
    public long StudentId { get; set; }
    public long? SocialServiceId { get; set; }
    public long? EligiblePeriodId { get; set; }
    public string? EligiblePeriodCode { get; set; }
    public long? TrackingPeriodId { get; set; }
    public string? TrackingPeriodCode { get; set; }
    public int? ElapsedSemesters { get; set; }
    public int ApprovedDocumentsCount { get; set; }
    public DateTimeOffset? LastApprovedAt { get; set; }
    public string Status { get; set; } = "pending_setup";
    public string? ProgressAssessment { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
public sealed class SocialServiceFollowUpReview : AuditableEntity
{
    public long SocialServiceId { get; set; }
    public long PeriodId { get; set; }
    public string Assessment { get; set; } = "consistent";
    public string? Notes { get; set; }
}
