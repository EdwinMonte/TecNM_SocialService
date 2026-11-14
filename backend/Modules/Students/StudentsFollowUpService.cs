using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
namespace TecNM.Api.Modules.Students;

public partial class StudentsService
{
    private async Task<long?> ResolveEligiblePeriodAsync(long? id, CancellationToken ct)
    {
        var period = await dbContext.AcademicPeriods.AsNoTracking().Where(p => id.HasValue ? p.Id == id : p.IsActive).OrderByDescending(p => p.StartDate).ThenByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (period == null) throw new ValidationException(new Dictionary<string, string[]> { { "socialServiceEligiblePeriodId", ["Selecciona un periodo válido o configura el periodo activo."] } });
        var active = await dbContext.AcademicPeriods.AsNoTracking().Where(p => p.IsActive).OrderByDescending(p => p.StartDate).ThenByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (active != null && period.StartDate > active.StartDate) throw new ValidationException(new Dictionary<string, string[]> { { "socialServiceEligiblePeriodId", ["El periodo no puede ser posterior al activo."] } });
        return period.Id;
    }
    private sealed record SocialServiceFollowUpSnapshot(string Status, DateOnly StartDate);

    private static (string? Assessment, string? AutoReason) ResolveFollowUpAssessment(
        StudentSocialServiceFollowUp f,
        SocialServiceFollowUpSnapshot? service,
        DateTimeOffset? activePeriodStartUtc,
        DateOnly graceStartDate,
        DateTimeOffset cutoffUtc)
    {
        if (f.Status != "in_progress") return (null, null);
        if (f.TrackingPeriodId == null || f.SocialServiceId == null || service == null) return (null, null);
        if (service.Status == "completed_pending_review") return ("consistent", "Trámite concluido pendiente de revisión");
        if (service.StartDate >= graceStartDate) return ("consistent", "Inicio reciente del servicio social (primeros 60 días)");
        if (f.LastApprovedAt.HasValue && ((activePeriodStartUtc.HasValue && f.LastApprovedAt.Value >= activePeriodStartUtc.Value) || f.LastApprovedAt.Value >= cutoffUtc))
            return ("consistent", "Con documento aprobado recientemente");
        return ("low_progress", "Sin documentos aprobados en el periodo activo o últimos 75 días");
    }

    private static SocialServiceFollowUpDto FollowUpDto(
        StudentSocialServiceFollowUp f,
        SocialServiceFollowUpSnapshot? service = null,
        DateTimeOffset? activePeriodStartUtc = null,
        DateOnly? graceStartDate = null,
        DateTimeOffset? cutoffUtc = null)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var (assessment, autoReason) = ResolveFollowUpAssessment(
            f,
            service,
            activePeriodStartUtc,
            graceStartDate ?? DateOnly.FromDateTime(nowUtc.UtcDateTime).AddDays(-60),
            cutoffUtc ?? nowUtc.AddDays(-75));
        return new(
            f.SocialServiceId,
            f.EligiblePeriodId,
            f.EligiblePeriodCode,
            f.TrackingPeriodId,
            f.TrackingPeriodCode,
            f.ElapsedSemesters,
            f.ApprovedDocumentsCount,
            f.LastApprovedAt,
            f.Status,
            assessment,
            autoReason);
    }
}
