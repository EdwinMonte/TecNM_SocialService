using System.Text.Json;
using System.Linq.Expressions;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Modules.Expedients;

public static class SocialServiceDocumentCatalog
{
    private sealed record Entry(string Number, string Title, string[] Codes);
    private static readonly Entry[] Entries = Load();

    private static Entry[] Load()
    {
        using var stream = typeof(SocialServiceDocumentCatalog).Assembly.GetManifestResourceStream(
            "TecNM.Api.Modules.Expedients.social-service-documents.json")
            ?? throw new InvalidOperationException("Falta el catálogo documental SS.");
        return JsonSerializer.Deserialize<Entry[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public static string Title(string code) => Entries.FirstOrDefault(e => e.Codes.Contains(code))?.Title ?? code;
    public static int Order(string code)
    {
        var index = Array.FindIndex(Entries, e => e.Codes.Contains(code));
        return index < 0 ? Entries.Length : index;
    }

    public static Expression<Func<ExpedientDocument, int>> SortExpression()
    {
        var document = Expression.Parameter(typeof(ExpedientDocument), "document");
        var code = Expression.Property(document, nameof(ExpedientDocument.DocumentCode));
        Expression rank = Expression.Constant(Entries.Length);
        for (var index = Entries.Length - 1; index >= 0; index--)
            foreach (var value in Entries[index].Codes)
                rank = Expression.Condition(Expression.Equal(code, Expression.Constant(value)), Expression.Constant(index), rank);
        rank = Expression.Condition(
            Expression.Equal(Expression.Property(document, nameof(ExpedientDocument.TramiteType)), Expression.Constant("social_service")),
            rank, Expression.Constant(Entries.Length));
        return Expression.Lambda<Func<ExpedientDocument, int>>(rank, document);
    }
}
