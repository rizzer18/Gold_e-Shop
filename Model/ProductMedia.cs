using Microsoft.AspNetCore.Mvc.Formatters;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gold_e_Shop.Model
{
    public class ProductMedia
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public string MediaUrl { get; set; } = null!;
        public string MediaType { get; set; } = null!;
    }
}
