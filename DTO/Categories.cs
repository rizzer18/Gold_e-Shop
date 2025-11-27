using System.ComponentModel.DataAnnotations;

public sealed class CreateCategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = null!;
}
