namespace TecNM.Api.Core.Entities;

public sealed class LinkageCompanyMetric
{
    public string TramiteType { get; set; } = "";
    public long PeriodId { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public string CompanyType { get; set; } = "";
    public long Projects { get; set; }
    public long AssignedStudents { get; set; }
    public long ActiveStudents { get; set; }
}

public sealed class LinkageTypeMetric
{
    public string TramiteType { get; set; } = "";
    public long PeriodId { get; set; }
    public string CompanyType { get; set; } = "";
    public long Projects { get; set; }
    public long AssignedStudents { get; set; }
    public long ActiveStudents { get; set; }
}
