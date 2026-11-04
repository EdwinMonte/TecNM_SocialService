namespace TecNM.Api.Core.Entities;

public class ExpedientDocument : AuditableEntity
{
    public uint Version { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public string Origin { get; set; } = "uploaded";
    public string? ContentSha256 { get; set; }
    public string? TemplateVersion { get; set; }
    public long StudentId { get; set; }
    public string TramiteType { get; set; } = string.Empty;
    public long? TramiteId { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string FileMimeType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = "pending";
    public string? RejectionReason { get; set; }
    public long? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public Student? Student { get; set; }
    public User? Reviewer { get; set; }
}
