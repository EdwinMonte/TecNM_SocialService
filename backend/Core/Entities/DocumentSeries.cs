namespace TecNM.Api.Core.Entities;

public sealed class DocumentSeries : AuditableEntity
{
    public uint Version { get; set; }
    public required string DocumentType { get; set; }
    public required string Prefix { get; set; }
    public int CurrentYear { get; set; }
    public int CurrentSequence { get; set; }
    public string FormatPattern { get; set; } = "{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}";
    public string? Description { get; set; }
}
