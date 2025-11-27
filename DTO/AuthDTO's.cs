namespace Gold_e_Shop.DTO
{
    public sealed class LoginRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public sealed class LoginResponse
    {
        public string Token { get; set; } = null!;
    }

    public sealed class RegisterAdminRequest
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

}
