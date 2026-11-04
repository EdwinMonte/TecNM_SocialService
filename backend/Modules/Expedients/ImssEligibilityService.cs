using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Expedients;

public sealed class ImssEligibilityService(TecNMDbContext db, TimeProvider clock)
{
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).DateTime);

    public static bool IsCurrent(ExpedientDocument? document, DateOnly today) => document is
        { TramiteType: "general", TramiteId: null, DocumentCode: "IMSS", Status: "approved", DeletedAt: null, DocumentDate: { } issued }
        && issued.Year == today.Year && issued <= today;

    public Task<ExpedientDocument?> GetLatestAsync(long studentId, CancellationToken ct = default) => db.ExpedientDocuments
        .Include(d => d.Reviewer).AsNoTracking()
        .Where(d => d.StudentId == studentId && d.TramiteType == "general" && d.DocumentCode == "IMSS" && d.Status != "superseded")
        .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).FirstOrDefaultAsync(ct);

    public async Task<long> RequireApprovedAsync(long studentId, CancellationToken ct = default)
    {
        var doc = await GetLatestAsync(studentId, ct);
        if (!IsCurrent(doc, Today(clock))) throw new BusinessRuleException("students.imss_approval_required",
            "Requiere una Constancia de Vigencia IMSS del año actual aprobada por Vinculación.");
        return doc!.Id;
    }

    public static void ValidateApprovalDate(DateOnly? issued, TimeProvider clock)
    {
        var today = Today(clock);
        if (!issued.HasValue || issued.Value.Year != today.Year || issued.Value > today)
            throw new ValidationException(new Dictionary<string, string[]> { ["documentDate"] = ["Confirma la fecha de expedición del año actual, sin fechas futuras."] });
    }
}
