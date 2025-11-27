namespace Gold_e_Shop.Services
{
    public interface IJwtTokenService
    {
        (string token, DateTime expiresUtc) CreateToken(int userId, string role, string? username = null);
    }
}
