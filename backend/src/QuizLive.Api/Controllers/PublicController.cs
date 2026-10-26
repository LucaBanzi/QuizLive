using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;
using QuizLive.Api.Dtos;

namespace QuizLive.Api.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController(QuizDbContext db, IConfiguration cfg) : ControllerBase
{
    /// <summary>Usato dal Display per il QR code.</summary>
    [HttpGet("info")]
    public async Task<PublicInfoDto> Info()
    {
        var name = await db.Sessions.AsNoTracking().Where(s => s.IsActive).Select(s => s.Name).FirstOrDefaultAsync() ?? "Quiz";
        return new PublicInfoDto(name, cfg["Quiz:PublicOrigin"] ?? "");
    }
}
