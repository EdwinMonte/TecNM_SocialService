using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Catalogs;

public partial class CareersService(TecNMDbContext dbContext)
{
    [GeneratedRegex(@"^[A-Z0-9_-]+$")]
    private static partial Regex CodeRegex();

    public async Task<PagedResponse<CareerResponse>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Careers.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Code, pattern) || EF.Functions.ILike(c.Name, pattern));
        }

        query = sort?.ToLowerInvariant() switch
        {
            "-code" => query.OrderByDescending(c => c.Code),
            "name" => query.OrderBy(c => c.Name),
            "-name" => query.OrderByDescending(c => c.Name),
            _ => query.OrderBy(c => c.Code)
        };

        var projected = query.Select(c => new CareerResponse(
            c.Id,
            c.Code,
            c.Name,
            c.TotalCredits,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt));

        return await projected.ToPagedResponseAsync(page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.Careers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Code, c.Name, c.TotalCredits })
            .ToListAsync(cancellationToken);

        return items
            .Select(c => new OptionItem(
                c.Id,
                $"{c.Code} · {c.Name}",
                new Dictionary<string, object?> { ["code"] = c.Code, ["totalCredits"] = c.TotalCredits }))
            .ToList();
    }

    public async Task<CareerResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var career = await dbContext.Careers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (career is null)
        {
            throw new NotFoundException("careers.not_found", "Carrera no encontrada.");
        }

        return ToResponse(career);
    }

    public async Task<CareerResponse> CreateAsync(CareerRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await dbContext.Careers
            .AnyAsync(c => c.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new ConflictException("careers.duplicated_code", $"Ya existe una carrera con la clave '{normalizedCode}'.");
        }

        var career = new Career
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            TotalCredits = request.TotalCredits,
            IsActive = true
        };

        dbContext.Careers.Add(career);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(career);
    }

    public async Task<CareerResponse> UpdateAsync(long id, CareerRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var career = await dbContext.Careers
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (career is null)
        {
            throw new NotFoundException("careers.not_found", "Carrera no encontrada.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeInUse = await dbContext.Careers
            .AnyAsync(c => c.Id != id && c.Code == normalizedCode, cancellationToken);

        if (codeInUse)
        {
            throw new ConflictException("careers.duplicated_code", $"Ya existe otra carrera con la clave '{normalizedCode}'.");
        }

        career.Code = normalizedCode;
        career.Name = request.Name.Trim();
        career.TotalCredits = request.TotalCredits;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(career);
    }

    public async Task<CareerResponse> UpdateStatusAsync(long id, bool isActive, CancellationToken cancellationToken = default)
    {
        var career = await dbContext.Careers
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (career is null)
        {
            throw new NotFoundException("careers.not_found", "Carrera no encontrada.");
        }

        career.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(career);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var career = await dbContext.Careers
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (career is null)
        {
            throw new NotFoundException("careers.not_found", "Carrera no encontrada.");
        }

        dbContext.Careers.Remove(career);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(CareerRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 16)
        {
            errors["code"] = ["La clave es obligatoria y debe tener entre 1 y 16 caracteres."];
        }
        else if (!CodeRegex().IsMatch(request.Code.Trim().ToUpperInvariant()))
        {
            errors["code"] = ["La clave solo puede contener letras mayúsculas, números, guiones y guiones bajos."];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150)
        {
            errors["name"] = ["El nombre es obligatorio y debe tener entre 1 y 150 caracteres."];
        }

        if (request.TotalCredits <= 0)
        {
            errors["totalCredits"] = ["El total de créditos debe ser un entero mayor a 0."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static CareerResponse ToResponse(Career c) => new(
        c.Id,
        c.Code,
        c.Name,
        c.TotalCredits,
        c.IsActive,
        c.CreatedAt,
        c.UpdatedAt);
}
