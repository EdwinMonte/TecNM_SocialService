namespace TecNM.Api.Core.Entities;

public sealed class User : AuditableEntity
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string FirstSurname { get; set; }
    public string? SecondSurname { get; set; }
    public string FullName => string.IsNullOrWhiteSpace(SecondSurname)
        ? $"{FirstName} {FirstSurname}"
        : $"{FirstName} {FirstSurname} {SecondSurname}";
    public bool IsActive { get; set; }
    public int SessionVersion { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
