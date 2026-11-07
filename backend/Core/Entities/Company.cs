namespace TecNM.Api.Core.Entities;

public sealed class Company : AuditableEntity
{
    public string? Rfc { get; set; }
    public required string Name { get; set; }
    public string? CommercialName { get; set; }
    public string CompanyType { get; set; } = null!;
    public string Sector { get; set; } = "private";
    public string CompanySize { get; set; } = "not_applicable";
    public bool IsTecnmInternal { get; set; }
    public string? Address { get; set; }
    public string City { get; set; } = "Monclova";
    public string State { get; set; } = "Coahuila";
    public string? PostalCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CompanySupervisor> Supervisors { get; set; } = [];
    public ICollection<Agreement> Agreements { get; set; } = [];
}
