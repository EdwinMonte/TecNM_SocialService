using TecNM.Api.Core.Entities;

namespace TecNM.Api.Modules.Expedients;

public static class DocumentProgressCalculator
{
    public static bool DocumentDone(Core.Entities.SocialService service, ISet<string> approved, string code)
    {
        if (code == "SOL") return service.Application?.IsAcceptedByOffice == true;
        if (approved.Contains(code)) return true;
        if (code == "COT") return service.Status == "released" || !string.IsNullOrWhiteSpace(service.ReleaseCertificateFolio);
        if (service.DocumentWorkflowVersion >= 2) return false;
        return code switch
        {
            "CP" => !string.IsNullOrWhiteSpace(service.PresentationLetterFolio) || service.PresentationLetterDate.HasValue,
            "CA" => service.AcceptanceLetterDate.HasValue,
            "SOL" => service.Application?.IsAcceptedByOffice == true,
            "CT" => service.TerminationLetterDate.HasValue,
            _ => false
        };
    }

    public static bool EvaluationDone(ISet<string> approved, int version, int period) => version >= 2
        ? approved.Contains($"EV_SUP{period}") && approved.Contains($"EV_SELF{period}") && (period != 3 || approved.Contains("EV_ACT"))
        : approved.Contains($"EV{period}");

    public static int SocialService(Core.Entities.SocialService service, ISet<string> approved)
    {
        var v2 = service.DocumentWorkflowVersion >= 2;
        var steps = new[]
        {
            DocumentDone(service, approved, "CP"),
            DocumentDone(service, approved, "CA"),
            DocumentDone(service, approved, "SOL"),
            approved.Contains("CC"), approved.Contains("R1"), approved.Contains("R2"), approved.Contains("R3"),
            v2 ? DocumentDefinitions.SeparateEvaluations.All(approved.Contains) : Enumerable.Range(1, 3).Any(n => EvaluationDone(approved, 1, n)),
            DocumentDone(service, approved, "CT"),
            DocumentDone(service, approved, "COT")
        };
        return steps.Count(done => done) * 10;
    }
}
