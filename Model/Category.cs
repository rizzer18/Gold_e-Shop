namespace Gold_e_Shop.Model
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!; // unique
        public int SortOrder { get; set; }
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
