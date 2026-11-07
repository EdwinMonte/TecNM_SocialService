namespace TecNM.Api.Core.Entities;

public static class CompanyTypes
{
    public static readonly string[] All = ["public", "micro", "small", "medium", "large", "social"];
    public static bool IsConsistent(string? type, string sector, string size) => All.Contains(type)
        && (sector == "educational" || sector == "public" && type == "public"
            || sector == "social" && type == "social" || sector == "private" && type == size);
}
