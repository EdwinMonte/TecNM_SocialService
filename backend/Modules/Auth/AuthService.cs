using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TecNM.Api.Core.Entities;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Auth;

public sealed record JwtSettings(
    string Issuer,
    string Audience,
    int LifetimeHours,
    SymmetricSecurityKey SigningKey);

public sealed class AuthService(
    TecNMDbContext dbContext,
    PasswordHasher<User> passwordHasher,
    JwtSettings jwtSettings)
{
    public async Task<AuthSession?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = username.Trim();
        var emailLogin = normalizedUsername.Contains('@');
        if (emailLogin) normalizedUsername = normalizedUsername.ToLowerInvariant();
        var user = await dbContext.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(
                item => emailLogin ? item.Username.ToLower() == normalizedUsername : item.Username == normalizedUsername,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var roles = user.UserRoles
            .Where(ur => ur.Role != null && ur.Role.DeletedAt == null)
            .Select(ur => ur.Role!.Name)
            .Distinct()
            .ToArray();

        var expiresAt = DateTimeOffset.UtcNow.AddHours(jwtSettings.LifetimeHours);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new("username", user.Username),
            new("sv", user.SessionVersion.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("roles", role));
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                jwtSettings.SigningKey,
                SecurityAlgorithms.HmacSha256));

        return new AuthSession(
            ToResponse(user, roles),
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }

    public UserResponse? GetCurrentUser(ClaimsPrincipal principal)
    {
        var idValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var username = principal.FindFirst("username")?.Value;
        var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var name = principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
        var roles = principal.FindAll("roles").Select(c => c.Value).Distinct().ToArray();
        if (roles.Length == 0)
        {
            roles = principal.FindAll("role").Select(c => c.Value).Distinct().ToArray();
        }

        if (!long.TryParse(idValue, CultureInfo.InvariantCulture, out var id)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new UserResponse(id, username, email, name, roles);
    }

    private static UserResponse ToResponse(User user, string[] roles) =>
        new(user.Id, user.Username, user.Email, user.FullName, roles);
}
