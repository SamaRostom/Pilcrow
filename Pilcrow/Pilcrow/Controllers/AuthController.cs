using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pilcrow.Data;
using Pilcrow.Dtos;
using Pilcrow.Models;
using Pilcrow.Services;

namespace Pilcrow.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public class AuthController(PilcrowDbContext db, TokenService tokens) : ControllerBase
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    [HttpPost("signup")]
    public async Task<ActionResult<AuthResponse>> Signup(SignupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || req.Password.Length < 8)
            return BadRequest("Email required, password must be at least 8 characters.");

        var taken = await db.Users.AnyAsync(u => u.Email == req.Email && u.DeletedAt == null);
        if (taken) return Conflict("An account with that email already exists.");

        var user = new User
        {
            Email        = req.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(req.Password),
            FullName     = req.FullName
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await IssueAsync(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email && u.DeletedAt == null);

        // Same response whether the email is unknown or the password is wrong,
        // so the endpoint cannot be used to discover which emails exist.
        if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid email or password.");

        return await IssueAsync(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        var hash = TokenService.Hash(req.RefreshToken);

        var token = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (token is null) return Unauthorized();

        // A revoked token being presented means someone kept a copy.
        // Revoke the whole chain rather than just refusing this request.
        if (token.RevokedAt is not null)
        {
            await RevokeChainAsync(token.Id);
            return Unauthorized("Token reuse detected. All sessions revoked.");
        }

        if (token.ExpiresAt < DateTimeOffset.UtcNow) return Unauthorized("Expired.");

        token.RevokedAt = DateTimeOffset.UtcNow;
        return await IssueAsync(token.User, parentId: token.Id);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        var hash = TokenService.Hash(req.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    private async Task<AuthResponse> IssueAsync(User user, Guid? parentId = null)
    {
        var (raw, hash) = TokenService.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId    = user.Id,
            TokenHash = hash,
            ParentId  = parentId,
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpHash    = HashIp(),
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshLifetime)
        });

        await db.SaveChangesAsync();

        return new AuthResponse(
            tokens.CreateAccessToken(user),
            raw,
            DateTimeOffset.UtcNow.AddMinutes(15));
    }

    private async Task RevokeChainAsync(Guid tokenId)
    {
        await db.Database.ExecuteSqlAsync($"""
            WITH RECURSIVE chain AS (
                SELECT id FROM refresh_tokens WHERE id = {tokenId}
                UNION ALL
                SELECT r.id FROM refresh_tokens r JOIN chain c ON r.parent_id = c.id
            )
            UPDATE refresh_tokens SET revoked_at = now()
            WHERE id IN (SELECT id FROM chain) AND revoked_at IS NULL
            """);
    }

    // The raw IP is personal data; the hash still supports abuse detection.
    private string? HashIp()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return ip is null ? null : TokenService.Hash(ip);
    }
}