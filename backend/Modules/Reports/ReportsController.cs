using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TecNM.Api.Modules.Reports;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "StatisticsRead")]
public sealed class ReportsController(ReportsService service) : ControllerBase
{
    [HttpGet("linkage")]
    public async Task<ActionResult<LinkageResponse>> GetLinkage(
        [FromQuery] string tramiteType, [FromQuery] long? periodId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await service.GetLinkageAsync(tramiteType, periodId, page, pageSize, cancellationToken));

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(
        [FromQuery] long? periodId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetDashboardAsync(periodId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("tracking")]
    public async Task<ActionResult<IReadOnlyList<TrackingRowResponse>>> GetTracking(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        [FromQuery] string? status = null,
        [FromQuery] long? careerId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetTrackingAsync(periodId, tramiteType, status, careerId, search, cancellationToken);
        return Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<StatisticalReportResponse>> GetStatistics(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        [FromQuery] long? comparePeriodId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetStatisticsAsync(periodId, comparePeriodId, tramiteType, cancellationToken);
        return Ok(result);
    }

    [HttpGet("911")]
    public async Task<ActionResult<Report911Response>> GetReport911(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        [FromQuery] long? comparePeriodId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetReport911Async(periodId, comparePeriodId, tramiteType, cancellationToken);
        return Ok(result);
    }

    [HttpGet("non-conformance-indicator")]
    public async Task<ActionResult<NonConformanceIndicatorResponse>> GetNonConformanceIndicator(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetNonConformanceIndicatorAsync(periodId, tramiteType, cancellationToken);
        return Ok(result);
    }

    [HttpGet("cycle-times")]
    public async Task<ActionResult<CycleTimesResponse>> GetCycleTimes(
        [FromQuery] string tramiteType = "social_service",
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetCycleTimesAsync(tramiteType, cancellationToken);
        return Ok(result);
    }
}

