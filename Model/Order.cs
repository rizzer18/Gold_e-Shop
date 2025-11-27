using System.ComponentModel.DataAnnotations.Schema;
using static Gold_e_Shop.Enums.Enums;

namespace Gold_e_Shop.Model
{
    public class Order
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int? GuestId { get; set; }
        public Guest? Guest { get; set; }

        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}
