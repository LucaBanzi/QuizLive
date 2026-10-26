using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;
using QuizLive.Api.Domain;
using QuizLive.Api.Dtos;
using QuizLive.Api.Services;

namespace QuizLive.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(QuizDbContext db, IPasswordHasher<AdminUser> hasher, TokenService tokens) : ControllerBase
{
    /// <summary>Rate limit 5 tentativi/min per IP (policy "login") e ritardo fisso di 500 ms sugli errori.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == "admin");
        var ok = user != null && !string.IsNullOrEmpty(req.Password) &&
                 hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) != PasswordVerificationResult.Failed;
        if (!ok)
        {
            await Task.Delay(500);
            return Unauthorized(new { error = "INVALID_CREDENTIALS" });
        }
        var (token, expires) = tokens.Create(user!.Username);
        return Ok(new LoginResponse(token, expires));
    }
}
