namespace Gold_e_Shop.Model
{
    public class Favorite
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int? GuestId { get; set; }
        public Guest? Guest { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
