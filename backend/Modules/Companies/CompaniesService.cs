using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Companies;

public partial class CompaniesService(TecNMDbContext dbContext)
{
    private static readonly HashSet<string> ValidSectors = ["public", "private", "social", "educational"];
    private static readonly HashSet<string> ValidSizes = ["micro", "small", "medium", "large", "not_applicable"];

    [GeneratedRegex(@"^[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}$")]
    private static partial Regex RfcRegex();

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCodeRegex();

    public async Task<PagedResponse<CompanyResponse>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? sector,
        string? companySize,
        bool? isTecnmInternal,
        bool? isActive,
        string? sort,
        string? companyType,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = dbContext.Companies.AsNoTracking();
        if (companyType is not null)
        {
            if (!CompanyTypes.All.Contains(companyType))
                throw new ValidationException(new Dictionary<string, string[]> { ["companyType"] = ["Selecciona un tipo de empresa válido."] });
            query = query.Where(c => c.CompanyType == companyType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(sector))
        {
            var normalizedSector = sector.Trim().ToLowerInvariant();
            query = query.Where(c => c.Sector == normalizedSector);
        }

        if (!string.IsNullOrWhiteSpace(companySize))
        {
            var normalizedSize = companySize.Trim().ToLowerInvariant();
            query = query.Where(c => c.CompanySize == normalizedSize);
        }

        if (isTecnmInternal.HasValue)
        {
            query = query.Where(c => c.IsTecnmInternal == isTecnmInternal.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern) ||
                (c.CommercialName != null && EF.Functions.ILike(c.CommercialName, pattern)) ||
                (c.Rfc != null && EF.Functions.ILike(c.Rfc, pattern)));
        }

        query = sort?.ToLowerInvariant() switch
        {
            "-name" => query.OrderByDescending(c => c.Name),
            "createdat" => query.OrderBy(c => c.CreatedAt),
            "-createdat" => query.OrderByDescending(c => c.CreatedAt),
            _ => query.OrderBy(c => c.Name)
        };

        var projected = query.Select(c => new CompanyResponse(
            c.Id,
            c.Rfc,
            c.Name,
            c.CommercialName,
            c.Sector,
            c.CompanySize,
            c.CompanyType,
            c.IsTecnmInternal,
            c.Address,
            c.City,
            c.State,
            c.PostalCode,
            c.PhoneNumber,
            c.Email,
            c.IsActive,
            c.Supervisors.Count(s => s.IsActive && s.DeletedAt == null),
            c.Agreements.Any(a => a.IsActive && a.DeletedAt == null && a.ExpirationDate >= today),
            c.CreatedAt,
            c.UpdatedAt));

        return await projected.ToPagedResponseAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);
    }

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        bool? isTecnmInternal,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = dbContext.Companies.AsNoTracking().Where(c => c.IsActive);

        if (isTecnmInternal.HasValue)
        {
            query = query.Where(c => c.IsTecnmInternal == isTecnmInternal.Value);
        }

        var items = await query
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.IsTecnmInternal,
                HasValidAgreement = c.Agreements.Any(a => a.IsActive && a.DeletedAt == null && a.ExpirationDate >= today)
            })
            .ToListAsync(cancellationToken);

        return items
            .Select(c => new OptionItem(
                c.Id,
                c.Name,
                new Dictionary<string, object?>
                {
                    ["isTecnmInternal"] = c.IsTecnmInternal,
                    ["hasValidAgreement"] = c.HasValidAgreement
                }))
            .ToList();
    }

    public async Task<CompanyResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var company = await dbContext.Companies
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CompanyResponse(
                c.Id,
                c.Rfc,
                c.Name,
                c.CommercialName,
                c.Sector,
                c.CompanySize,
            c.CompanyType,
                c.IsTecnmInternal,
                c.Address,
                c.City,
                c.State,
                c.PostalCode,
                c.PhoneNumber,
                c.Email,
                c.IsActive,
                c.Supervisors.Count(s => s.IsActive && s.DeletedAt == null),
                c.Agreements.Any(a => a.IsActive && a.DeletedAt == null && a.ExpirationDate >= today),
                c.CreatedAt,
                c.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            throw new NotFoundException("companies.not_found", $"Empresa con id {id} no encontrada.");
        }

        return company;
    }

    public async Task<CompanyResponse> CreateAsync(CompanyRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        string? normalizedRfc = null;
        if (!string.IsNullOrWhiteSpace(request.Rfc))
        {
            normalizedRfc = request.Rfc.Trim().ToUpperInvariant();
            var rfcExists = await dbContext.Companies
                .AnyAsync(c => c.Rfc == normalizedRfc, cancellationToken);

            if (rfcExists)
            {
                throw new ConflictException("companies.duplicated_rfc", $"Ya existe una empresa registrada con el RFC '{normalizedRfc}'.");
            }
        }

        var company = new Company
        {
            Rfc = normalizedRfc,
            Name = request.Name.Trim(),
            CommercialName = string.IsNullOrWhiteSpace(request.CommercialName) ? null : request.CommercialName.Trim(),
            Sector = request.Sector.Trim().ToLowerInvariant(),
            CompanySize = request.CompanySize.Trim().ToLowerInvariant(),
            CompanyType = request.CompanyType,
            IsTecnmInternal = request.IsTecnmInternal,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? "Monclova" : request.City.Trim(),
            State = string.IsNullOrWhiteSpace(request.State) ? "Coahuila" : request.State.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            IsActive = true
        };

        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(company.Id, cancellationToken);
    }

    public async Task<CompanyResponse> UpdateAsync(long id, CompanyRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var company = await dbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            throw new NotFoundException("companies.not_found", $"Empresa con id {id} no encontrada.");
        }

        string? normalizedRfc = null;
        if (!string.IsNullOrWhiteSpace(request.Rfc))
        {
            normalizedRfc = request.Rfc.Trim().ToUpperInvariant();
            var rfcExists = await dbContext.Companies
                .AnyAsync(c => c.Id != id && c.Rfc == normalizedRfc, cancellationToken);

            if (rfcExists)
            {
                throw new ConflictException("companies.duplicated_rfc", $"Ya existe otra empresa registrada con el RFC '{normalizedRfc}'.");
            }
        }

        company.Rfc = normalizedRfc;
        company.Name = request.Name.Trim();
        company.CommercialName = string.IsNullOrWhiteSpace(request.CommercialName) ? null : request.CommercialName.Trim();
        company.Sector = request.Sector.Trim().ToLowerInvariant();
        company.CompanySize = request.CompanySize.Trim().ToLowerInvariant();
        company.CompanyType = request.CompanyType;
        company.IsTecnmInternal = request.IsTecnmInternal;
        company.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        company.City = string.IsNullOrWhiteSpace(request.City) ? "Monclova" : request.City.Trim();
        company.State = string.IsNullOrWhiteSpace(request.State) ? "Coahuila" : request.State.Trim();
        company.PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim();
        company.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        company.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(company.Id, cancellationToken);
    }

    public async Task<CompanyResponse> UpdateStatusAsync(long id, bool isActive, CancellationToken cancellationToken = default)
    {
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            throw new NotFoundException("companies.not_found", $"Empresa con id {id} no encontrada.");
        }

        company.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(company.Id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            throw new NotFoundException("companies.not_found", $"Empresa con id {id} no encontrada.");
        }

        var hasSupervisors = await dbContext.CompanySupervisors
            .AnyAsync(s => s.CompanyId == id, cancellationToken);

        if (hasSupervisors)
        {
            throw new ConflictException("companies.in_use", "No es posible eliminar la empresa porque tiene supervisores vinculados.");
        }

        var hasAgreements = await dbContext.Agreements
            .AnyAsync(a => a.CompanyId == id, cancellationToken);

        if (hasAgreements)
        {
            throw new ConflictException("companies.in_use", "No es posible eliminar la empresa porque tiene convenios vinculados.");
        }

        if (await dbContext.SocialServices.AnyAsync(t => t.CompanyId == id, cancellationToken)
            || await dbContext.Internships.AnyAsync(t => t.CompanyId == id, cancellationToken))
            throw new ConflictException("companies.in_use", "No es posible eliminar una empresa con trámites vinculados.");

        dbContext.Companies.Remove(company);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(CompanyRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            errors["name"] = ["El nombre es obligatorio y debe tener entre 1 y 200 caracteres."];
        }

        if (request.CommercialName?.Trim().Length > 200)
        {
            errors["commercialName"] = ["El nombre comercial no puede exceder los 200 caracteres."];
        }

        var sector = request.Sector?.Trim().ToLowerInvariant() ?? "";
        if (!ValidSectors.Contains(sector))
        {
            errors["sector"] = [$"El sector '{request.Sector}' no es válido. Opciones: {string.Join(", ", ValidSectors)}."];
        }

        var size = request.CompanySize?.Trim().ToLowerInvariant() ?? "";
        if (!CompanyTypes.IsConsistent(request.CompanyType, sector, size))
            errors["companyType"] = ["Selecciona uno de los seis tipos compatible con el sector y tamaño."];
        if (!ValidSizes.Contains(size))
        {
            errors["companySize"] = [$"El tamaño de empresa '{request.CompanySize}' no es válido. Opciones: {string.Join(", ", ValidSizes)}."];
        }

        // Regla 911 / SEP: si sector=private, companySize no puede ser not_applicable
        if (sector == "private" && (size == "not_applicable" || string.IsNullOrWhiteSpace(size)))
        {
            throw new BusinessRuleException(
                "companies.size_required",
                "El tamaño de la empresa es obligatorio para empresas del sector privado (regla oficial 911).",
                new Dictionary<string, string[]>
                {
                    ["companySize"] = ["El tamaño de empresa no puede ser 'not_applicable' para empresas del sector privado."]
                });
        }

        if (!string.IsNullOrWhiteSpace(request.Rfc))
        {
            var rfc = request.Rfc.Trim().ToUpperInvariant();
            if (rfc.Length is < 12 or > 13 || !RfcRegex().IsMatch(rfc))
            {
                errors["rfc"] = ["El RFC no tiene un formato válido (12 o 13 caracteres alfanuméricos oficiales)."];
            }
        }

        if (!string.IsNullOrWhiteSpace(request.PostalCode) && !PostalCodeRegex().IsMatch(request.PostalCode.Trim()))
        {
            errors["postalCode"] = ["El código postal debe contener exactamente 5 dígitos numéricos."];
        }

        if (request.PhoneNumber?.Trim().Length > 30)
        {
            errors["phoneNumber"] = ["El número de teléfono no puede exceder los 30 caracteres."];
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (email.Length > 255 || !email.Contains('@') || !email.Contains('.'))
            {
                errors["email"] = ["El correo electrónico no es válido o supera los 255 caracteres."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
