using static Gold_e_Shop.Enums.Enums;

namespace Gold_e_Shop.Model
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }            // numeric(10,2)
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ImageUrl { get; set; }
        public int Likes { get; set; }
        public string? Specification { get; set; }
        public List<string> Materials { get; set; } = new List<string>();
        public decimal? Weight { get; set; }          // numeric(10,2)
        public int SortOrder { get; set; } = 0;

        public ICollection<ProductMedia> Media { get; set; } = new List<ProductMedia>();
        public ICollection<Cart> Carts { get; set; } = new List<Cart>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
