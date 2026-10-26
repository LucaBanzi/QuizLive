using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Domain;

namespace QuizLive.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(QuizDbContext db, IPasswordHasher<AdminUser> hasher, IConfiguration cfg, ILogger logger)
    {
        var password = cfg["Admin:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Admin__Password non configurata (ADMIN_PASSWORD nel file .env).");

        var admin = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == "admin");
        if (admin == null)
        {
            admin = new AdminUser { Username = "admin" };
            admin.PasswordHash = hasher.HashPassword(admin, password);
            db.AdminUsers.Add(admin);
            logger.LogInformation("Creato utente admin.");
        }
        else if (hasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            // la password nel .env è cambiata: la rendiamo effettiva al riavvio
            admin.PasswordHash = hasher.HashPassword(admin, password);
            logger.LogInformation("Password admin aggiornata dalla configurazione.");
        }

        if (!await db.Sessions.AnyAsync(s => s.IsActive))
            db.Sessions.Add(new QuizSession { Name = "Serata Quiz", IsActive = true });

        await db.SaveChangesAsync();
    }
}
