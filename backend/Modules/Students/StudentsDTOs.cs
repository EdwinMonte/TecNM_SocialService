namespace TecNM.Api.Modules.Students;

public sealed record StudentListItem(
    long Id,
    string ControlNumber,
    string FullName,
    long CareerId,
    string CareerCode,
    int? CurrentSemester,
    decimal? CurriculumProgressPercentage,
    string? Shift,
    bool? IsEnrolled,
    bool? HasValidImss,
    bool? HasUserAccount)
{
    public string? AccountUsername { get; init; }
    public string? AccountStatus { get; init; } = "missing";
    public bool CanReadProfile {get;init;} = true;
    public SocialServiceFollowUpDto? SocialServiceFollowUp {get;init;}
}

public sealed record StudentDetail(
    long Id,
    long? UserId,
    long CareerId,
    string CareerName,
    long? ImportBatchId,
    string ControlNumber,
    string? Curp,
    string FirstName,
    string FirstSurname,
    string? SecondSurname,
    string? Gender,
    string Shift,
    bool HasDisability,
    string? DisabilityDescription,
    string? InstitutionalEmail,
    string? PersonalEmail,
    string? PhoneNumber,
    int CurrentSemester,
    int CompletedCredits,
    decimal CurriculumProgressPercentage,
    bool IsEnrolled,
    bool HasValidImss,
    bool HasImssFile,
    DateTimeOffset? ImssValidatedAt,
    string? ImssValidatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public string? AccountUsername { get; init; }
    public string AccountStatus { get; init; } = "missing";
    public long? SocialServiceEligiblePeriodId {get;init;}
    public string ImssStatus { get; init; } = "missing";
    public long? ImssDocumentId { get; init; }
    public DateOnly? ImssDocumentDate { get; init; }
    public string? ImssRejectionReason { get; init; }
}

public sealed record StudentRequest(
    long CareerId,
    string ControlNumber,
    string? Curp,
    string FirstName,
    string FirstSurname,
    string? SecondSurname,
    string? Gender,
    string Shift = "morning",
    bool HasDisability = false,
    string? DisabilityDescription = null,
    string? InstitutionalEmail = null,
    string? PersonalEmail = null,
    string? PhoneNumber = null,
    int CurrentSemester = 7,
    int CompletedCredits = 156,
    bool IsEnrolled = true,
    long? SocialServiceEligiblePeriodId = null);

public sealed record StudentContactRequest(
    string? PersonalEmail,
    string? InstitutionalEmail,
    string? PhoneNumber,
    int? CurrentSemester = null,
    string? Shift = null);

public sealed record ValidateImssRequest(bool IsValid, string? RejectionReason = null, DateOnly? DocumentDate = null);

public sealed record CreateStudentAccountResponse(
    long UserId,
    string Username,
    string AccountStatus);

public sealed record EligibilityReason(string Code, string Message);

public sealed record EligibilityResult(
    bool Eligible,
    IReadOnlyList<EligibilityReason> Reasons);

public sealed record StudentEligibility(
    EligibilityResult? SocialService,
    EligibilityResult? Internship);

public sealed record StudentImportBatchDto(
    long Id,
    long PeriodId,
    string PeriodCode,
    string FileName,
    int TotalRecords,
    int ImportedRecords,
    int FailedRecords,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? CreatedByName) { public int CreatedAccounts { get; init; } public int ExistingAccounts { get; init; } }

public sealed record StudentImportRowError(
    int Row,
    string? ControlNumber,
    string? Field,
    string Message);

public sealed record StudentImportResult(
    long Id,
    long PeriodId,
    string PeriodCode,
    string FileName,
    int TotalRecords,
    int ImportedRecords,
    int FailedRecords,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? CreatedByName,
    IReadOnlyList<StudentImportRowError> RowErrors) { public int CreatedAccounts { get; init; } public int ExistingAccounts { get; init; } }


public sealed record SocialServiceFollowUpDto(long? SocialServiceId, long? EligiblePeriodId, string? EligiblePeriodCode, long? TrackingPeriodId, string? TrackingPeriodCode, int? ElapsedSemesters, int ApprovedDocumentsCount, DateTimeOffset? LastApprovedAt, string Status, string? ProgressAssessment, string? AutoReason = null);
