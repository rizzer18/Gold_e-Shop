namespace Gold_e_Shop.Model
{
    public class Guest
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = null!; // unique
        public DateTime CreatedAt { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }

        public ICollection<Cart> Carts { get; set; } = new List<Cart>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
