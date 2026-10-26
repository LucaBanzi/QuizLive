using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizLive.Api.Domain;

namespace QuizLive.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/media")]
public class MediaController(IConfiguration cfg) : ControllerBase
{
    private const long MaxBytes = 100L * 1024 * 1024;

    private static readonly Dictionary<string, (MediaType Type, string[] ContentTypes)> Allowed = new()
    {
        [".jpg"] = (MediaType.Image, new[] { "image/jpeg" }),
        [".jpeg"] = (MediaType.Image, new[] { "image/jpeg" }),
        [".png"] = (MediaType.Image, new[] { "image/png" }),
        [".webp"] = (MediaType.Image, new[] { "image/webp" }),
        [".gif"] = (MediaType.Image, new[] { "image/gif" }),
        [".mp3"] = (MediaType.Audio, new[] { "audio/mpeg", "audio/mp3" }),
        [".ogg"] = (MediaType.Audio, new[] { "audio/ogg", "application/ogg" }),
        [".wav"] = (MediaType.Audio, new[] { "audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave" }),
        [".mp4"] = (MediaType.Video, new[] { "video/mp4" }),
        [".webm"] = (MediaType.Video, new[] { "video/webm" }),
    };

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "File mancante." });
        if (file.Length > MaxBytes) return BadRequest(new { error = "File troppo grande (max 100 MB)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.TryGetValue(ext, out var rule)) return BadRequest(new { error = "Estensione non ammessa." });
        if (!rule.ContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return BadRequest(new { error = "Content-type non coerente con l'estensione." });

        var header = new byte[16];
        int read;
        await using (var s = file.OpenReadStream()) read = await s.ReadAsync(header.AsMemory(0, 16));
        if (!SignatureMatches(ext, header, read)) return BadRequest(new { error = "Il contenuto del file non corrisponde al formato dichiarato." });

        var dir = cfg["Media:Path"] ?? "media";
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";                    // nome rigenerato: mai quello dell'utente
        await using (var fs = System.IO.File.Create(Path.Combine(dir, name)))
            await file.CopyToAsync(fs);

        return Ok(new { url = $"/media/{name}", mediaType = rule.Type });
    }

    private static bool StartsWith(byte[] h, int read, params byte[] sig) =>
        read >= sig.Length && h.Take(sig.Length).SequenceEqual(sig);

    private static bool SignatureMatches(string ext, byte[] h, int read)
    {
        switch (ext)
        {
            case ".jpg": case ".jpeg": return StartsWith(h, read, 0xFF, 0xD8, 0xFF);
            case ".png": return StartsWith(h, read, 0x89, 0x50, 0x4E, 0x47);
            case ".gif": return StartsWith(h, read, 0x47, 0x49, 0x46, 0x38);
            case ".webp": return read >= 12 && StartsWith(h, read, 0x52, 0x49, 0x46, 0x46) && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50;
            case ".wav": return read >= 12 && StartsWith(h, read, 0x52, 0x49, 0x46, 0x46) && h[8] == 0x57 && h[9] == 0x41 && h[10] == 0x56 && h[11] == 0x45;
            case ".ogg": return StartsWith(h, read, 0x4F, 0x67, 0x67, 0x53);
            case ".mp3": return StartsWith(h, read, 0x49, 0x44, 0x33) || (read >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0);
            case ".mp4": return read >= 8 && h[4] == 0x66 && h[5] == 0x74 && h[6] == 0x79 && h[7] == 0x70;   // "ftyp"
            case ".webm": return StartsWith(h, read, 0x1A, 0x45, 0xDF, 0xA3);
            default: return false;
        }
    }
}
