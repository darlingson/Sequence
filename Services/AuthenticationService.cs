using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sequence.Contracts.Authentication;
using Sequence.Data;
using Sequence.Models;
using Sequence.Options;
using Sequence.Services.Interfaces;

namespace Sequence.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _jwt;

    public AuthenticationService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _userManager = userManager;
        _jwt = jwtOptions.Value;
    }

    public async Task<UserProfileResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ",
                result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "User");

        return await BuildProfileAsync(user);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
                   ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return await IssueTokenPairAsync(user);
    }

    public async Task<TokenResponse> RefreshAsync(string refreshTokenString)
    {
        var hash = HashToken(refreshTokenString);

        var stored = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null)
            throw new SecurityTokenException("Invalid refresh token.");

        if (stored.RevokedAt is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, "Refresh token reuse detected");
            throw new SecurityTokenException("Refresh token has been revoked.");
        }

        if (!stored.IsActive)
            throw new SecurityTokenException("Refresh token expired.");

        var user = await _userManager.FindByIdAsync(stored.UserId)
                   ?? throw new SecurityTokenException("User not found.");

        var (newToken, plainToken) = CreateRefreshTokenEntity(user.Id, stored.FamilyId);
        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedReason = "Rotated";
        stored.ReplacedByTokenId = newToken.Id;

        _context.RefreshTokens.Add(newToken);
        await _context.SaveChangesAsync();

        var (accessToken, accessExpiresAt) = await GenerateAccessTokenAsync(user);

        return new TokenResponse(accessToken, plainToken, accessExpiresAt, newToken.ExpiresAt);
    }

    public async Task<UserProfileResponse> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
                   ?? throw new InvalidOperationException("User not found.");
        return await BuildProfileAsync(user);
    }

    private async Task<TokenResponse> IssueTokenPairAsync(ApplicationUser user)
    {
        var (accessToken, accessExpiresAt) = await GenerateAccessTokenAsync(user);
        var (refreshEntity, plainToken) = CreateRefreshTokenEntity(user.Id, Guid.NewGuid());

        _context.RefreshTokens.Add(refreshEntity);
        await _context.SaveChangesAsync();

        return new TokenResponse(accessToken, plainToken, accessExpiresAt, refreshEntity.ExpiresAt);
    }

    private async Task<(string Token, DateTime ExpiresAt)> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var roles       = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user.Id);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("firstName", user.FirstName),
            new("lastName",  user.LastName)
        };

        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenLifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private (RefreshToken Entity, string PlainToken) CreateRefreshTokenEntity(string userId, Guid familyId)
    {
        var plain = GenerateRandomToken();

        var entity = new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            TokenHash = HashToken(plain),
            FamilyId  = familyId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenLifetimeDays)
        };

        return (entity, plain);
    }

    private async Task<List<string>> GetUserPermissionsAsync(string userId)
    {
        var roleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        return await _context.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync();
    }

    private async Task<UserProfileResponse> BuildProfileAsync(ApplicationUser user)
    {
        var roles       = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user.Id);

        return new UserProfileResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            roles,
            permissions);
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var t in tokens)
        {
            t.RevokedAt    = DateTime.UtcNow;
            t.RevokedReason = reason;
        }

        await _context.SaveChangesAsync();
    }

    private static string GenerateRandomToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}