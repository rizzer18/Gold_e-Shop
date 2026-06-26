using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace Gold_e_Shop.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContentController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public ContentController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string GetFilePath()
        {
            var root = Path.Combine(_env.ContentRootPath, "uploads");
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            return Path.Combine(root, "content.json");
        }

        private string ToPublicUrl(string relativeOrAbsoluteUrl)
        {
            if (string.IsNullOrEmpty(relativeOrAbsoluteUrl)) return "";
            if (relativeOrAbsoluteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                relativeOrAbsoluteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return relativeOrAbsoluteUrl;
            }
            if (relativeOrAbsoluteUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var req = HttpContext.Request;
                return $"{req.Scheme}://{req.Host}{relativeOrAbsoluteUrl}";
            }
            return relativeOrAbsoluteUrl;
        }

        [HttpGet]
        public async Task<IActionResult> GetContent()
        {
            var path = GetFilePath();
            if (!System.IO.File.Exists(path))
            {
                var defaultContent = new Dictionary<string, string>
                {
                    { "heroTitle", "Výroba autorských šperků" },
                    { "heroText", "Autorské šperky vznikající v tichu rukou a záměru. Každý kus je originál – osobní talisman inspirovaný přírodou, symbolikou a vnitřní silou. Vyber si šperk, který s tebou rezonuje." },
                    { "heroImageUrl", "/jewelry1.jpg" },
                    { "aboutTitle", "Jovana Šichová" },
                    { "aboutText", "Vytvářím autorské šperky, které v sobě nesou příběh, sílu a harmonii. Každý kousek je vyroben ručně s důrazem na detail, osobitý charakter a kvalitu zpracování. Inspiraci čerpám z přírodních tvarů, mystiky a syrové krásy ušlechtilých kovů." },
                    { "aboutImageUrl", "/utils/images/exampleAbout.jpg" },
                    { "card1Title", "Prsteny" },
                    { "card1Text", "Ručně kované detaily" },
                    { "card1ImageUrl", "/jewelry2.jpg" },
                    { "card2Title", "Přívěsky" },
                    { "card2Text", "Inspirace přírodou" },
                    { "card2ImageUrl", "/jewelry3.jpg" },
                    { "card3Title", "Náušnice" },
                    { "card3Text", "Elegance a harmonie" },
                    { "card3ImageUrl", "/utils/images/image1.png" }
                };

                var jsonStr = JsonSerializer.Serialize(defaultContent, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(path, jsonStr);
            }

            var contentBytes = await System.IO.File.ReadAllBytesAsync(path);
            var content = JsonSerializer.Deserialize<Dictionary<string, string>>(contentBytes);
            if (content == null) return BadRequest("Invalid content json");

            var resolvedContent = new Dictionary<string, string>();
            foreach (var kvp in content)
            {
                if (kvp.Key.EndsWith("ImageUrl", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedContent[kvp.Key] = ToPublicUrl(kvp.Value);
                }
                else
                {
                    resolvedContent[kvp.Key] = kvp.Value;
                }
            }

            return Ok(resolvedContent);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateContent([FromBody] Dictionary<string, string> newContent)
        {
            var path = GetFilePath();
            Dictionary<string, string> currentContent = new();
            if (System.IO.File.Exists(path))
            {
                var contentBytes = await System.IO.File.ReadAllBytesAsync(path);
                currentContent = JsonSerializer.Deserialize<Dictionary<string, string>>(contentBytes) ?? new();
            }

            foreach (var kvp in newContent)
            {
                var val = kvp.Value;
                if (val.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(val);
                        if (uri.PathAndQuery.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                        {
                            val = uri.PathAndQuery;
                        }
                    }
                    catch { }
                }
                currentContent[kvp.Key] = val;
            }

            var jsonStr = JsonSerializer.Serialize(currentContent, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(path, jsonStr);
            return Ok(new { message = "Content updated successfully" });
        }

        [Authorize(Roles = "admin")]
        [HttpPost("upload-image")]
        [RequestSizeLimit(1024L * 1024 * 100)]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("File is empty");

            var root = Path.Combine(_env.ContentRootPath, "uploads");
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);

            var ext = Path.GetExtension(file.FileName);
            var name = $"content-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(root, name);

            using (var fs = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(fs);
            }

            var relUrl = $"/uploads/{name}";
            var publicUrl = ToPublicUrl(relUrl);

            return Ok(new { url = publicUrl });
        }
    }
}
