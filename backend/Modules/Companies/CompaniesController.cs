using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Companies;

[ApiController]
[Route("api/companies")]
public class CompaniesController(CompaniesService companiesService) : ControllerBase
{

    [Authorize(Policy = "CompaniesRead")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CompanyResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sector = null,
        [FromQuery] string? companySize = null,
        [FromQuery] bool? isTecnmInternal = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? companyType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await companiesService.GetPagedAsync(
            page, pageSize, search, sector, companySize, isTecnmInternal, isActive, sort, companyType, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(
        [FromQuery] bool? isTecnmInternal = null,
        CancellationToken cancellationToken = default)
    {
        var options = await companiesService.GetOptionsAsync(isTecnmInternal, cancellationToken);
        return Ok(options);
    }

    [Authorize(Policy = "CompaniesRead")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<CompanyResponse>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var company = await companiesService.GetByIdAsync(id, cancellationToken);
        return Ok(company);
    }

    [Authorize(Policy = "CompaniesWrite")]
    [HttpPost]
    public async Task<ActionResult<CompanyResponse>> Create(
        [FromBody] CompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await companiesService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = "CompaniesWrite")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<CompanyResponse>> Update(
        long id,
        [FromBody] CompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await companiesService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Policy = "CompaniesWrite")]
    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<CompanyResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await companiesService.UpdateStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Policy = "CompaniesWrite")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        await companiesService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
