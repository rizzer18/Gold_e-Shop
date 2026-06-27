// Controllers/CategoriesController.cs
using Gold_e_Shop.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")] // => /api/categories
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db) => _db = db;

    // GET /api/categories
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name
            })
            .ToListAsync();

        return Ok(categories);
    }

    // PUT /api/categories/reorder   (admin)
    [Authorize(Roles = "admin")]
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<int> categoryIds)
    {
        if (categoryIds == null || categoryIds.Count == 0)
            return BadRequest(new { message = "Invalid category IDs" });

        var categories = await _db.Categories.Where(c => categoryIds.Contains(c.Id)).ToListAsync();
        
        for (int i = 0; i < categoryIds.Count; i++)
        {
            var catId = categoryIds[i];
            var cat = categories.FirstOrDefault(c => c.Id == catId);
            if (cat != null)
            {
                cat.SortOrder = i;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Categories reordered successfully" });
    }

    // POST /api/categories   (admin)
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateCategoryRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var entity = new Category { Name = req.Name };

        _db.Categories.Add(entity);
        try
        {
            await _db.SaveChangesAsync();
            return StatusCode(201, new { message = "Category added successfully", categoryId = entity.Id });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("categories_name_key", StringComparison.OrdinalIgnoreCase) == true)
        {
            // UNIQUE (Name)
            return Conflict(new { message = "Category name already exists" });
        }
    }

    // DELETE /api/categories/{id}   (admin)
    [Authorize(Roles = "admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return NotFound(new { message = "Category not found" });

        foreach (var p in cat.Products)
        {
            p.CategoryId = null;
        }

        _db.Categories.Remove(cat);
        try
        {
            await _db.SaveChangesAsync();
            return Ok(new { message = "Category deleted successfully" });
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            var isFk = msg.Contains("foreign key", StringComparison.OrdinalIgnoreCase)
                       || msg.Contains("fk_products_category_id", StringComparison.OrdinalIgnoreCase)
                       || msg.Contains("23503"); // Postgres FK code

            if (isFk)
                return Conflict(new { message = "Cannot delete category with related products" });

            return StatusCode(500, new { message = "Error deleting category", error = ex.Message });
        }
    }
}
