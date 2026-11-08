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
        { TramiteType: "general", TramiteId: null, DocumentCode: "IMSS", Status: "approved", DeletedAt: null };

    public Task<ExpedientDocument?> GetLatestAsync(long studentId, CancellationToken ct = default) => db.ExpedientDocuments
        .Include(d => d.Reviewer).AsNoTracking()
        .Where(d => d.StudentId == studentId && d.TramiteType == "general" && d.DocumentCode == "IMSS" && d.Status != "superseded")
        .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).FirstOrDefaultAsync(ct);

    public async Task<long> RequireApprovedAsync(long studentId, CancellationToken ct = default)
    {
        var doc = await GetLatestAsync(studentId, ct);
        if (!IsCurrent(doc, Today(clock))) throw new BusinessRuleException("students.imss_approval_required",
            "Requiere una Constancia de Vigencia IMSS con estado VIGENTE aprobada por Vinculación.");
        return doc!.Id;
    }

}
