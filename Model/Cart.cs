namespace Gold_e_Shop.Model
{
    public class Cart
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int? GuestId { get; set; }
        public Guest? Guest { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
