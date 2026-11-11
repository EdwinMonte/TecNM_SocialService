using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Modules.Expedients.Pdf;
using TecNM.Api.Modules.SocialService;
using TecNM.Api.Modules.Internships;

namespace TecNM.Api.Modules.Expedients;

[ApiController]
[Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head,student")]
public sealed class DocumentTemplatesController(OfficialPdfService pdf, SocialServiceService socialService, InternshipsService internships, DocumentsService documents) : ControllerBase
{
    [HttpGet("/api/document-templates/{key}/file")]
    public IActionResult Template(string key)
    {
        if (key is not ("ss-supervisor-evaluation" or "ss-self-evaluation" or "ss-termination" or "ss-acceptance-example"))
            throw new NotFoundException("documents.template_not_found", "La plantilla no existe.");
        var result = pdf.Template(key);
        return File(result.Bytes, "application/pdf", result.FileName);
    }

    [HttpGet("/api/social-services/{id:long}/pdf/{document}")]
    public async Task<IActionResult> SocialServicePdf(long id, string document, CancellationToken ct)
    {
        if (document == "individual_act")
        {
            var actData = await socialService.GetIndividualActDataAsync(id, ct);
            return File(pdf.Generate("ss-individual-act", actData), "application/pdf", $"SS_{id}_acta_individual.pdf");
        }
        var detail = await socialService.GetByIdAsync(id, ct);
        if (document == "presentation_letter")
        {
            var files = await documents.GetStudentDocumentsAsync(detail.Student.Id, "social_service", id, "approved", false, ct);
            var issued = files.SingleOrDefault(d => d.DocumentCode == "CP")
                ?? throw new ConflictException("documents.template_unavailable", "Carga la Carta de Presentación firmada y espera su aprobación documental.");
            var (stream, name) = await documents.GetDocumentStreamAsync(issued.Id, ct);
            return File(stream, "application/pdf", name);
        }
        var key = document switch
        {
            "application" => "ss-application",
            "commitment" => "ss-commitment",
            "report_1" or "report_2" or "report_3" => "ss-report",
            "evaluation_1" or "evaluation_2" or "evaluation_3" => "ss-supervisor-evaluation",
            "self_evaluation_1" or "self_evaluation_2" or "self_evaluation_3" => "ss-self-evaluation",
            "activity_assessment" => "ss-activity",
            "termination_letter" => "ss-termination",
            _ => throw new ConflictException("documents.template_unavailable", "La plantilla oficial aún no está disponible.")
        };
        if (document.StartsWith("report_") && !detail.Reports.Any(r => r.ReportNumber == int.Parse(document[^1..])))
            throw new BusinessRuleException("documents.data_missing", "Guarda primero las fechas, horas y actividades del reporte.");
        var data = await socialService.GetPrintDataAsync(id, document, ct);
        data["realizationPeriod"] = $"{detail.StartDate:dd/MM/yyyy} - {detail.EndDate:dd/MM/yyyy}";
        data["finalMark"] = "X";
        // A missing second surname is valid, unlike an absent student name.
        if (data.TryGetValue("studentSecondSurname", out var surname) && surname is null) data["studentSecondSurname"] = " ";
        return File(pdf.Generate(key, data), "application/pdf", $"SS_{id}_{document}.pdf");
    }

    [HttpGet("/api/internships/{id:long}/pdf/evaluation")]
    public async Task<IActionResult> InternshipPdf(long id, CancellationToken ct)
    {
        var data = await internships.GetEvaluationFormatDataAsync(id, ct);
        return File(pdf.Generate("pp-evaluation", data), "application/pdf", $"PP_{id}_evaluacion.pdf");
    }
}
