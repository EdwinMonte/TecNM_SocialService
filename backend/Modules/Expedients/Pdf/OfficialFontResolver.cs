using PdfSharp.Fonts;
using TecNM.Api.Core.Exceptions;

namespace TecNM.Api.Modules.Expedients.Pdf;

public sealed class OfficialFontResolver(string root) : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(familyName == "Arial" ? "arial.ttf" : "GOTHIC.TTF");

    public byte[] GetFont(string faceName)
    {
        var path = Path.Combine(root, faceName);
        if (!File.Exists(path)) throw new ConflictException("documents.font_unavailable",
            "Configura las fuentes autorizadas para generar este formato.");
        return File.ReadAllBytes(path);
    }
}
