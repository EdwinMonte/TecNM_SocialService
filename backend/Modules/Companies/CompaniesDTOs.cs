namespace TecNM.Api.Modules.Companies;

// --- DTOs para Empresas ---
public sealed record CompanyResponse(
    long Id,
    string? Rfc,
    string Name,
    string? CommercialName,
    string Sector,
    string CompanySize,
    string CompanyType,
    bool IsTecnmInternal,
    string? Address,
    string City,
    string State,
    string? PostalCode,
    string? PhoneNumber,
    string? Email,
    bool IsActive,
    int ActiveSupervisorsCount,
    bool HasValidAgreement,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CompanyRequest(
    string? Rfc,
    string Name,
    string? CommercialName,
    string Sector,
    string CompanySize,
    string CompanyType,
    bool IsTecnmInternal,
    string? Address,
    string City,
    string State,
    string? PostalCode,
    string? PhoneNumber,
    string? Email);

public sealed record UpdateStatusRequest(bool IsActive);

// --- DTOs para Supervisores ---
public sealed record CompanySupervisorResponse(
    long Id,
    long CompanyId,
    string FullName,
    string Position,
    string? Department,
    string? Email,
    string? PhoneNumber,
    bool IsSignatory,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CompanySupervisorRequest(
    string FullName,
    string Position,
    string? Department,
    string? Email,
    string? PhoneNumber,
    bool IsSignatory = true);

// --- DTOs para Convenios ---
public sealed record AgreementResponse(
    long Id,
    long CompanyId,
    string CompanyName,
    string AgreementNumber,
    string AgreementType,
    string? Description,
    DateOnly SignedDate,
    DateOnly ExpirationDate,
    bool IsActive,
    bool HasDocument,
    string Validity,
    int DaysToExpiration,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AgreementRequest(
    long CompanyId,
    string AgreementNumber,
    string AgreementType,
    string? Description,
    DateOnly SignedDate,
    DateOnly ExpirationDate);
