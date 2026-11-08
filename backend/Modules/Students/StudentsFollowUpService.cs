using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Security;
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
    private static SocialServiceFollowUpDto FollowUpDto(StudentSocialServiceFollowUp f) => new(f.SocialServiceId, f.EligiblePeriodId, f.EligiblePeriodCode, f.TrackingPeriodId, f.TrackingPeriodCode, f.ElapsedSemesters, f.ApprovedDocumentsCount, f.LastApprovedAt, f.Status, f.Status == "in_progress" ? f.ProgressAssessment : null, f.Status == "in_progress" ? f.Notes : null, f.Status == "in_progress" ? f.ReviewedAt : null);
    public async Task<SocialServiceFollowUpDto> ReviewFollowUpAsync(long id, FollowUpReviewRequest request, CancellationToken ct)
    {
        currentUser.RequireProcess("social_service");
        if (!currentUser.IsInRole("admin") && !currentUser.IsInRole("social_service_officer")) throw new AppException(403, "auth.forbidden", "No puedes valorar seguimiento.");
        if (request.Assessment is not ("consistent" or "low_progress") || request.Notes?.Length > 1000) throw new ValidationException(new Dictionary<string, string[]> { { "assessment", ["Valoración inválida u observación mayor a 1000 caracteres."] } });
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var f = await dbContext.StudentSocialServiceFollowUps.SingleOrDefaultAsync(f => f.StudentId == id, ct) ?? throw new NotFoundException("students.not_found", "Alumno no encontrado.");
        if (f.TrackingPeriodId != request.PeriodId || f.Status != "in_progress" || f.SocialServiceId == null) throw new ConflictException("students.follow_up_not_reviewable", "Se requiere el periodo activo y evidencia SS aprobada.");
        var review = await dbContext.SocialServiceFollowUpReviews.SingleOrDefaultAsync(r => r.SocialServiceId == f.SocialServiceId && r.PeriodId == request.PeriodId, ct);
        if (review == null) { review = new SocialServiceFollowUpReview { SocialServiceId = f.SocialServiceId.Value, PeriodId = request.PeriodId }; dbContext.SocialServiceFollowUpReviews.Add(review); }
        review.Assessment = request.Assessment; review.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        try { await dbContext.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" }) { throw new ConflictException("students.follow_up_conflict", "La valoración cambió. Actualiza e inténtalo de nuevo."); }
        return FollowUpDto(await dbContext.StudentSocialServiceFollowUps.AsNoTracking().SingleAsync(x => x.StudentId == id, ct));
    }
}
