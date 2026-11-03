using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Catalogs;

public class AcademicPeriodsService(TecNMDbContext dbContext)
{
    public async Task<PagedResponse<AcademicPeriodResponse>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AcademicPeriods.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(ap => ap.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(ap => EF.Functions.ILike(ap.Code, pattern) || EF.Functions.ILike(ap.Name, pattern));
        }

        query = sort?.ToLowerInvariant() switch
        {
            "code" => query.OrderBy(ap => ap.Code),
            "-code" => query.OrderByDescending(ap => ap.Code),
            "startdate" => query.OrderBy(ap => ap.StartDate),
            _ => query.OrderByDescending(ap => ap.StartDate)
        };

        var projected = query.Select(ap => new AcademicPeriodResponse(
            ap.Id,
            ap.Code,
            ap.Name,
            ap.StartDate,
            ap.EndDate,
            ap.IsActive,
            ap.CreatedAt,
            ap.UpdatedAt));

        return await projected.ToPagedResponseAsync(page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.AcademicPeriods
            .AsNoTracking()
            .OrderByDescending(ap => ap.IsActive)
            .ThenByDescending(ap => ap.StartDate)
            .Select(ap => new { ap.Id, ap.Code, ap.Name, ap.IsActive })
            .ToListAsync(cancellationToken);

        return items
            .Select(ap => new OptionItem(
                ap.Id,
                ap.IsActive ? $"{ap.Code} · {ap.Name} (Activo)" : $"{ap.Code} · {ap.Name}",
                new Dictionary<string, object?> { ["isActive"] = ap.IsActive, ["code"] = ap.Code }))
            .ToList();
    }

    public async Task<AcademicPeriodResponse> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var current = await dbContext.AcademicPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(ap => ap.IsActive, cancellationToken);

        if (current is null)
        {
            throw new NotFoundException("academic_periods.no_current", "No hay ningún periodo académico activo en este momento.");
        }

        return ToResponse(current);
    }

    public async Task<AcademicPeriodResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var period = await dbContext.AcademicPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(ap => ap.Id == id, cancellationToken);

        if (period is null)
        {
            throw new NotFoundException("academic_periods.not_found", "Periodo académico no encontrado.");
        }

        return ToResponse(period);
    }

    public async Task<AcademicPeriodResponse> CreateAsync(AcademicPeriodRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await dbContext.AcademicPeriods
            .AnyAsync(ap => ap.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new ConflictException("academic_periods.duplicated_code", $"Ya existe un periodo con la clave '{normalizedCode}'.");
        }

        var period = new AcademicPeriod
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = false // Inicialmente inactivo según spec
        };

        dbContext.AcademicPeriods.Add(period);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(period);
    }

    public async Task<AcademicPeriodResponse> UpdateAsync(long id, AcademicPeriodRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var period = await dbContext.AcademicPeriods
            .SingleOrDefaultAsync(ap => ap.Id == id, cancellationToken);

        if (period is null)
        {
            throw new NotFoundException("academic_periods.not_found", "Periodo académico no encontrado.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeInUse = await dbContext.AcademicPeriods
            .AnyAsync(ap => ap.Id != id && ap.Code == normalizedCode, cancellationToken);

        if (codeInUse)
        {
            throw new ConflictException("academic_periods.duplicated_code", $"Ya existe otro periodo con la clave '{normalizedCode}'.");
        }

        period.Code = normalizedCode;
        period.Name = request.Name.Trim();
        period.StartDate = request.StartDate;
        period.EndDate = request.EndDate;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(period);
    }

    public async Task<AcademicPeriodResponse> ActivateAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var period = await dbContext.AcademicPeriods
            .SingleOrDefaultAsync(ap => ap.Id == id, cancellationToken);

        if (period is null)
        {
            throw new NotFoundException("academic_periods.not_found", "Periodo académico no encontrado.");
        }

        // Desactivar todos los demás periodos
        var activePeriods = await dbContext.AcademicPeriods
            .Where(ap => ap.Id != id && ap.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var p in activePeriods)
        {
            p.IsActive = false;
        }

        period.IsActive = true;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(period);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var period = await dbContext.AcademicPeriods
            .SingleOrDefaultAsync(ap => ap.Id == id, cancellationToken);

        if (period is null)
        {
            throw new NotFoundException("academic_periods.not_found", "Periodo académico no encontrado.");
        }

        dbContext.AcademicPeriods.Remove(period);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(AcademicPeriodRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 32)
        {
            errors["code"] = ["La clave es obligatoria y debe tener entre 1 y 32 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            errors["name"] = ["El nombre es obligatorio y debe tener entre 1 y 100 caracteres."];
        }

        if (request.StartDate > request.EndDate)
        {
            errors["endDate"] = ["La fecha de fin no puede ser anterior a la fecha de inicio."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static AcademicPeriodResponse ToResponse(AcademicPeriod ap) => new(
        ap.Id,
        ap.Code,
        ap.Name,
        ap.StartDate,
        ap.EndDate,
        ap.IsActive,
        ap.CreatedAt,
        ap.UpdatedAt);
}
