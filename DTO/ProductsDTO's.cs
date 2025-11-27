public sealed class ProductListResponse
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalProducts { get; set; }
    public IEnumerable<object> Products { get; set; } = Array.Empty<object>();
}

public sealed class CreateUpdateProductRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int? CategoryId { get; set; }
    public int Stock { get; set; } = 0;
    public string? Specification { get; set; }
    public string? Material { get; set; }
    public decimal? Weight { get; set; }
}
