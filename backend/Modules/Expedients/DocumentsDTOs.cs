using Microsoft.AspNetCore.Http;

namespace TecNM.Api.Modules.Expedients;

public sealed record ExpedientDocumentResponse(
    long Id,
    long StudentId,
    string TramiteType,
    long? TramiteId,
    string DocumentCode,
    string OriginalFileName,
    string FileMimeType,
    long FileSizeBytes,
    string Status,
    string? RejectionReason,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt,
    DateOnly? DocumentDate = null,
    string Origin = "uploaded",
    string? TemplateVersion = null
);

public sealed record ReviewQueueDocumentResponse(
    long Id,
    long StudentId,
    string StudentControlNumber,
    string StudentFullName,
    string CareerCode,
    string CareerName,
    string TramiteType,
    long? TramiteId,
    string DocumentCode,
    string OriginalFileName,
    string FileMimeType,
    long FileSizeBytes,
    string Status,
    string? RejectionReason,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt,
    DateOnly? DocumentDate = null
);

public sealed class UploadDocumentFormRequest
{
    public IFormFile? File { get; set; }
    public string TramiteType { get; set; } = string.Empty;
    public long? TramiteId { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public DateOnly? DocumentDate { get; set; }
}

public sealed record ReviewDocumentRequest(
    string Status,
    string? RejectionReason,
    DateOnly? DocumentDate = null
);
