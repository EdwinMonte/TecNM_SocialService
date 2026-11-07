using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Reports;

public sealed record LinkageTotals(long AssignedStudents, long ActiveStudents, long Projects);
public sealed record LinkageTypeRow(string CompanyType, long AssignedStudents, long ActiveStudents, long Projects);
public sealed record LinkageCompanyRow(long CompanyId, string CompanyName, string CompanyType, long Projects, long AssignedStudents, long ActiveStudents);
public sealed record LinkageResponse(string TramiteType, ReportPeriodSummary Period, LinkageTotals Totals,
    IReadOnlyList<LinkageTypeRow> ByCompanyType, PagedResponse<LinkageCompanyRow> Companies);

public sealed partial class ReportsService
{
    public async Task<LinkageResponse> GetLinkageAsync(string tramiteType, long? periodId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var type = ValidateTramiteType(tramiteType);
        var period = await dbContext.AcademicPeriods.AsNoTracking()
            .Where(p => periodId.HasValue ? p.Id == periodId.Value : p.IsActive)
            .OrderByDescending(p => p.StartDate).ThenBy(p => p.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("academic_periods.not_found", "No existe el periodo solicitado o no hay un periodo activo.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var query = dbContext.Set<LinkageCompanyMetric>().Where(m => m.TramiteType == type && m.PeriodId == period.Id);
        var typeQuery = dbContext.Set<LinkageTypeMetric>().Where(m => m.TramiteType == type && m.PeriodId == period.Id);
        var totals = await typeQuery.GroupBy(m => m.PeriodId)
            .Select(g => new LinkageTotals(g.Sum(m => m.AssignedStudents), g.Sum(m => m.ActiveStudents), g.Sum(m => m.Projects)))
            .SingleOrDefaultAsync(ct) ?? new LinkageTotals(0, 0, 0);
        var groups = await typeQuery.Select(m => new LinkageTypeRow(m.CompanyType, m.AssignedStudents, m.ActiveStudents, m.Projects)).ToListAsync(ct);
        var companies = await query.OrderBy(m => m.CompanyName).ThenBy(m => m.CompanyId)
            .Select(m => new LinkageCompanyRow(m.CompanyId, m.CompanyName, m.CompanyType, m.Projects, m.AssignedStudents, m.ActiveStudents))
            .ToPagedResponseAsync(page, pageSize, ct);
        await transaction.CommitAsync(ct);
        var distribution = CompanyTypes.All.Select(code => groups.SingleOrDefault(g => g.CompanyType == code) ?? new LinkageTypeRow(code, 0, 0, 0)).ToList();
        return new(type, new(period.Id, period.Code, period.Name), totals, distribution, companies);
    }
}
