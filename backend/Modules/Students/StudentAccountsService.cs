using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Students;

public sealed class StudentAccountsService(TecNMDbContext db, PasswordHasher<User> hasher)
{
    public static string NormalizeEmail(string? value)
    {
        var email = value?.Trim().ToLowerInvariant() ?? "";
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new ValidationException(new Dictionary<string, string[]> { ["institutionalEmail"] = ["Indica un correo institucional válido (máximo 254 caracteres)."] });
        return email;
    }

    public static string Status(Student student)
    {
        if (student.UserId is null) return "missing";
        var user = student.User;
        if (user is null || user.DeletedAt is not null || !HasStudentRoleOnly(user)) return "inconsistent";
        return user.IsActive ? "active" : "inactive";
    }

    private static bool HasStudentRoleOnly(User user)
    {
        var roles = user.UserRoles.Where(r => r.DeletedAt is null && r.Role is { DeletedAt: null }).Select(r => r.Role!.Name).Distinct().ToArray();
        return roles.Length == 1 && roles[0] == "student";
    }

    // The caller owns the transaction: academic data, user, role and link commit together.
    public async Task<bool> EnsureAsync(Student student, CancellationToken ct)
    {
        var email = NormalizeEmail(student.InstitutionalEmail);
        if (await db.Students.AnyAsync(s => s.Id != student.Id && s.InstitutionalEmail != null && s.InstitutionalEmail.Trim().ToLower() == email, ct)
            || await db.Users.AnyAsync(u => u.Id != student.UserId && (u.Username.ToLower() == email || u.Email.ToLower() == email), ct))
            throw new ConflictException("students.account_email_conflict", "El correo institucional ya pertenece a otro alumno o cuenta.");

        student.InstitutionalEmail = email;
        if (student.UserId.HasValue)
        {
            var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).SingleOrDefaultAsync(u => u.Id == student.UserId, ct);
            if (user is null || !HasStudentRoleOnly(user))
                throw new ConflictException("students.account_inconsistent", "La cuenta vinculada requiere revisión administrativa; no se modificó su acceso.");
            if (user.Username != email || user.Email != email) user.SessionVersion++;
            user.Username = email;
            user.Email = email;
            user.FirstName = student.FirstName;
            user.FirstSurname = student.FirstSurname;
            user.SecondSurname = student.SecondSurname;
            student.User = user;
            return false;
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == "student", ct)
            ?? throw new NotFoundException("roles.not_found", "No existe el rol student.");
        var account = new User { Username = email, Email = email, PasswordHash = "", FirstName = student.FirstName,
            FirstSurname = student.FirstSurname, SecondSurname = student.SecondSurname, IsActive = true };
        account.PasswordHash = hasher.HashPassword(account, student.ControlNumber.Trim().ToUpperInvariant());
        db.Users.Add(account);
        db.UserRoles.Add(new UserRole { User = account, RoleId = role.Id, Role = role });
        student.User = account;
        return true;
    }

    public async Task ResetPasswordAsync(long studentId, CancellationToken ct)
    {
        var student = await db.Students.Include(s => s.User).ThenInclude(u => u!.UserRoles).ThenInclude(r => r.Role)
            .SingleOrDefaultAsync(s => s.Id == studentId, ct)
            ?? throw new NotFoundException("students.not_found", "No existe el alumno.");
        if (Status(student) is "missing" or "inconsistent")
            throw new ConflictException("students.account_inconsistent", "El alumno no tiene una cuenta consistente para restablecer.");
        student.User!.PasswordHash = hasher.HashPassword(student.User, student.ControlNumber.Trim().ToUpperInvariant());
        student.User.SessionVersion++;
        await db.SaveChangesAsync(ct);
    }
}
