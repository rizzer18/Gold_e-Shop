namespace Gold_e_Shop.DTO
{
    public class CreateOrderForGuestRequest
    {
        public GuestInfoDto GuestInfo { get; set; } = null!;
        public List<CartItemDto> CartItems { get; set; } = new();
    }

    public class GuestInfoDto
    {
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public AddressDto Address { get; set; } = null!;
    }

    public class AddressDto
    {
        public string Street { get; set; } = null!;
        public string City { get; set; } = null!;
        public string HouseNumber { get; set; } = null!;
        public string Country { get; set; } = null!;
    }

    public class CartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
    public class UpdateOrderDto
    {
        public string? Status { get; set; }
        public decimal? Total_Amount { get; set; } 
    }


}
