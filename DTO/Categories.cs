using System.ComponentModel.DataAnnotations;

public sealed class CreateCategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = null!;
}
public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
