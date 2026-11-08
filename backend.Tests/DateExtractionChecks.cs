using PdfSharp.Drawing;
using PdfSharp.Pdf;
using TecNM.Api.Modules.Expedients;

internal static class DateExtractionChecks
{
    public static void TextChecks()
    {
        void Check(string[] text, string status, DateOnly? date = null)
        {
            var result = ImssDateExtractionService.FromText(text);
            if (result.Status != status || result.SuggestedDate != date) throw new Exception($"IMSS date extraction: expected {status}/{date}, got {result}");
        }
        Check(["Nacimiento: 01/01/2000\nFecha de expedición: 03/11/2026\nAlta: 01/02/2026"], "found", new(2026,11,3));
        Check(["Fecha de emisión\n2025-12-31"], "found", new(2025,12,31));
        Check(["FECHA DE EXPEDICIÓN: 3 de noviembre de 2026"], "found", new(2026,11,3));
        Check(["Fecha de emisión: 2027-01-01"], "found", new(2027,1,1));
        Check(["Fecha de expedición: 03/11/2026", "Fecha de emisión: 04/11/2026"], "ambiguous");
        Check(["Fecha de expedición: 31/02/2026"], "ambiguous");
        Check(["Fecha de expedición: 03/11/2026", "Fecha de emisión: 03/11/2026"], "found", new(2026,11,3));
        Check(["Nacimiento: 03/11/2026"], "not_found");
        Check([""], "not_found");
        Check(Enumerable.Repeat("",11).ToArray(), "unavailable");
        Check([new string('x',200001)], "unavailable");
        if (ImssDateExtractionService.Extract(new MemoryStream([1,2,3])).Status != "unavailable") throw new Exception("Broken PDF must permit manual fallback.");
    }

    public static byte[] Pdf(string[] lines, int pages = 1)
    {
        using var document = new PdfDocument();
        for (var i=0;i<pages;i++)
        {
            var page=document.AddPage();
            using var graphics=XGraphics.FromPdfPage(page);
            for(var line=0;line<lines.Length;line++) graphics.DrawString(lines[line],new XFont("Arial",12),XBrushes.Black,new XPoint(40,50+line*25));
        }
        using var output=new MemoryStream(); document.Save(output); return output.ToArray();
    }

    public static void PdfChecks()
    {
        var result=ImssDateExtractionService.Extract(new MemoryStream(Pdf(["Nacimiento: 01/01/2000", "Fecha de expedición: 03/11/2026"])));
        if(result.Status!="found" || result.SuggestedDate!=new DateOnly(2026,11,3) || result.PageNumber!=1) throw new Exception($"Digital PDF extraction failed: {result}");
        if(ImssDateExtractionService.Extract(new MemoryStream(Pdf([],11))).Status!="unavailable") throw new Exception("PDF page limit missing.");
        if(ImssDateExtractionService.Extract(new MemoryStream(Pdf([]))).Status!="not_found") throw new Exception("Image-only/empty page must use manual capture.");
        Console.WriteLine("IMSS digital PDF, contextual dates, ambiguity and manual fallback checks passed.");
    }
}
