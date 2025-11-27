// Controllers/ProductsController.cs
using Gold_e_Shop.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // GET /api/products?page=1&limit=10
    [HttpGet]
    public async Task<ActionResult<ProductListResponse>> GetAll([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        page = page <= 0 ? 1 : page;
        limit = limit <= 0 ? 10 : limit;

        var total = await _db.Products.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)limit);

        // 1) ТІЛЬКИ ДАНІ з БД
        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Media)
            .OrderBy(p => p.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        // 2) КЛІЄНТСЬКА проєкція (поза EF)
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var items = products.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.CategoryId,
            p.Stock,
            p.CreatedAt,
            p.ImageUrl,
            p.Likes,
            p.Specification,
            p.Material,
            p.Weight,
            mediaUrls = p.Media.Select(m => baseUrl + m.MediaUrl).ToArray()
        });

        return new ProductListResponse
        {
            Page = page,
            TotalPages = totalPages,
            TotalProducts = total,
            Products = items.ToList()
        };
    }


    // GET /api/products/category/{categoryId}?page=1&limit=10
    [HttpGet("category/{categoryId:int}")]
    public async Task<ActionResult<ProductListResponse>> GetByCategory(int categoryId, [FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        page = page <= 0 ? 1 : page;
        limit = limit <= 0 ? 10 : limit;

        var query = _db.Products.Where(p => p.CategoryId == categoryId);
        var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)limit);

        var items = await query
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.CategoryId,
                p.Stock,
                p.CreatedAt,
                p.ImageUrl,
                p.Likes,
                p.Specification,
                p.Material,
                p.Weight,
                mediaUrls = p.Media.Select(m => ToPublicUrl(m.MediaUrl)).ToArray()
            })
            .ToListAsync();

        return new ProductListResponse
        {
            Page = page,
            TotalPages = totalPages,
            TotalProducts = total,
            Products = items
        };
    }

    // GET /api/products/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _db.Products
            .AsNoTracking()
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound(new { message = "Product not found" });

        return Ok(new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.CategoryId,
            p.Stock,
            p.CreatedAt,
            p.ImageUrl,
            p.Likes,
            p.Specification,
            p.Material,
            p.Weight,
            mediaUrls = p.Media.Select(m => ToPublicUrl(m.MediaUrl)).ToArray()
        });
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    [RequestSizeLimit(1024L * 1024 * 100)] 
    public async Task<IActionResult> Add([FromForm] CreateUpdateProductRequest req, [FromForm] IFormFileCollection? media)
    {
        var product = new Product
        {
            Name = req.Name,
            Description = req.Description,
            Price = req.Price,
            CategoryId = req.CategoryId,
            Stock = req.Stock,
            Specification = req.Specification,
            Material = req.Material,
            Weight = req.Weight,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        if (media is { Count: > 0 })
            await SaveMediaFilesAsync(product.Id, media);

        return StatusCode(201, new { message = "Product added successfully", productId = product.Id });
    }

    // PUT /api/products/{id} (multipart/form-data; додає нові media, не видаляє старі)
    [Authorize(Roles = "admin")]
    [HttpPut("{id:int}")]
    [RequestSizeLimit(1024L * 1024 * 100)]
    public async Task<IActionResult> Update(int id, [FromForm] CreateUpdateProductRequest req, [FromForm] IFormFileCollection? media)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound(new { message = "Product not found" });

        p.Name = req.Name;
        p.Description = req.Description;
        p.Price = req.Price;
        p.CategoryId = req.CategoryId;
        p.Stock = req.Stock;
        p.Specification = req.Specification;
        p.Material = req.Material;
        p.Weight = req.Weight;

        await _db.SaveChangesAsync();

        if (media is { Count: > 0 })
            await SaveMediaFilesAsync(p.Id, media);

        return Ok(new { message = "Product updated successfully" });
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        // order_items
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM order_items WHERE \"ProductId\" = {0}", id);

        // media: забрати шляхи файлів -> стерти з диска -> видалити записи
        var media = await _db.ProductMedia.Where(m => m.ProductId == id).ToListAsync();
        foreach (var m in media)
            TryDeletePhysicalFile(m.MediaUrl);

        _db.ProductMedia.RemoveRange(media);

        // product
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound(new { message = "Product not found" });

        _db.Products.Remove(p);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Product and all related records deleted successfully" });
    }

    // ---- helpers ----
    private async Task SaveMediaFilesAsync(int productId, IFormFileCollection media)
    {
        var root = Path.Combine(_env.ContentRootPath, "uploads");
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);

        foreach (var file in media)
        {
            if (file.Length <= 0) continue;

            var ext = Path.GetExtension(file.FileName);
            var name = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(root, name);

            using (var fs = System.IO.File.Create(fullPath))
                await file.CopyToAsync(fs);

            var relUrl = $"/uploads/{name}"; // для клієнта
            var mediaType = file.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase) ? "image" : "video";

            _db.ProductMedia.Add(new ProductMedia
            {
                ProductId = productId,
                MediaUrl = relUrl,    // зберігаємо /uploads/xxx
                MediaType = mediaType // string
            });
        }
        await _db.SaveChangesAsync();
    }

    private void TryDeletePhysicalFile(string storedUrl)
    {
        // storedUrl у нас вигляду "/uploads/xxxx.ext"
        var root = Path.Combine(_env.ContentRootPath, "uploads");
        var fileName = Path.GetFileName(storedUrl);
        var fullPath = Path.Combine(root, fileName);
        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); } catch { /* ignore */ }
        }
    }

    private string ToPublicUrl(string storedUrl)
    {
        var req = HttpContext.Request;
        return $"{req.Scheme}://{req.Host}{storedUrl}";
    }
}
