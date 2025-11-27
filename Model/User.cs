using System.ComponentModel.DataAnnotations.Schema;
using static Gold_e_Shop.Enums.Enums;
namespace Gold_e_Shop.Model
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!; // unique
        public string Password { get; set; } = null!;
        public string Email { get; set; } = null!;    // unique
        public string Role { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public ICollection<Cart> Carts { get; set; } = new List<Cart>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
