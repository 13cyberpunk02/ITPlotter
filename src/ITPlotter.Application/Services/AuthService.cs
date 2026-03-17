using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ITPlotter.Application.DTOs.Auth;
using ITPlotter.Application.Interfaces;
using ITPlotter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ITPlotter.Application.Services;

public class AuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthService(IApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email, ct))
            throw new InvalidOperationException("Пользователь с таким email уже существует.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await GenerateTokens(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct)
            ?? throw new UnauthorizedAccessException("Неверный email или пароль.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Неверный email или пароль.");

        return await GenerateTokens(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && !rt.IsRevoked, ct)
            ?? throw new UnauthorizedAccessException("Невалидный refresh token.");

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            storedToken.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Refresh token истёк.");
        }

        storedToken.IsRevoked = true;
        await _db.SaveChangesAsync(ct);

        return await GenerateTokens(storedToken.User, ct);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("Пользователь не найден.");

        return new UserDto(user.Id, user.FirstName, user.LastName, user.Email, user.PhotoPath, user.Role);
    }

    private async Task<AuthResponse> GenerateTokens(User user, CancellationToken ct)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "30"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(jwtSettings["RefreshTokenExpirationDays"] ?? "7")),
            UserId = user.Id
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken.Token, expires);
    }
}
