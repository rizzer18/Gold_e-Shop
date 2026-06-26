using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Gold_e_Shop.Model;

namespace Gold_e_Shop.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CartController(AppDbContext db)
        {
            _db = db;
        }

        public class AddToCartRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; } = 1;
        }

        public class UpdateCartRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public class RemoveFromCartRequest
        {
            public int ProductId { get; set; }
        }

        private string? ToPublicUrl(string? mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
                return null;

            var fileName = mediaUrl.TrimStart('/', '\\');
            if (fileName.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName["uploads/".Length..];
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/uploads/{fileName}";
        }

        private Task<(int? userId, string? sessionId)> GetUserOrSessionAsync()
        {
            // If logged in via JWT
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                return Task.FromResult<(int? userId, string? sessionId)>((userId, null));
            }

            // Try header first (sent by frontend using localStorage-based session)
            if (Request.Headers.TryGetValue("X-Guest-Session-Id", out var headerSessionId) && !string.IsNullOrEmpty(headerSessionId))
            {
                return Task.FromResult<(int? userId, string? sessionId)>((null, headerSessionId.ToString()));
            }

            // Otherwise, get guest session ID from cookie
            if (Request.Cookies.TryGetValue("guest_session_id", out var sessionId))
            {
                return Task.FromResult<(int? userId, string? sessionId)>((null, sessionId));
            }

            // Generate new guest session ID if none exists
            sessionId = Guid.NewGuid().ToString();
            Response.Cookies.Append("guest_session_id", sessionId, new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                Path = "/"
            });

            return Task.FromResult<(int? userId, string? sessionId)>((null, sessionId));
        }

        private async Task<Guest> GetOrCreateGuestAsync(string sessionId)
        {
            var guest = await _db.Guests.FirstOrDefaultAsync(g => g.SessionId == sessionId);
            if (guest == null)
            {
                guest = new Guest
                {
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    Name = "Guest",
                    Surname = "Guest",
                    Email = "",
                    PhoneNumber = "",
                    Street = "",
                    City = "",
                    HouseNumber = "",
                    Country = ""
                };
                _db.Guests.Add(guest);
                await _db.SaveChangesAsync();
            }
            return guest;
        }

        // GET /api/cart
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var (userId, sessionId) = await GetUserOrSessionAsync();
            List<Model.Cart> cartItems;

            if (userId.HasValue)
            {
                cartItems = await _db.Cart
                    .Include(c => c.Product)
                    .ThenInclude(p => p.Media)
                    .Where(c => c.UserId == userId.Value)
                    .ToListAsync();
            }
            else
            {
                var guest = await GetOrCreateGuestAsync(sessionId!);
                cartItems = await _db.Cart
                    .Include(c => c.Product)
                    .ThenInclude(p => p.Media)
                    .Where(c => c.GuestId == guest.Id)
                    .ToListAsync();
            }

            var result = cartItems.Select(c => new
            {
                id = c.ProductId.ToString(),
                name = c.Product.Name,
                description = c.Product.Description,
                price = c.Product.Price,
                category_id = c.Product.CategoryId.ToString(),
                stock = c.Product.Stock.ToString(),
                quantity = c.Quantity,
                mediaUrls = c.Product.Media.Select(m => ToPublicUrl(m.MediaUrl)).ToArray()
            });

            return Ok(result);
        }

        // POST /api/cart/add
        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest req)
        {
            var product = await _db.Products.FindAsync(req.ProductId);
            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            var (userId, sessionId) = await GetUserOrSessionAsync();
            Model.Cart? cartItem;

            if (userId.HasValue)
            {
                cartItem = await _db.Cart.FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == req.ProductId);
                int alreadyInCart = cartItem?.Quantity ?? 0;
                if (alreadyInCart + req.Quantity > product.Stock)
                {
                    return BadRequest(new { error = $"Nedostatečný sklad. Na skladě je {product.Stock} ks, v košíku již máte {alreadyInCart} ks." });
                }

                if (cartItem == null)
                {
                    cartItem = new Model.Cart
                    {
                        UserId = userId.Value,
                        ProductId = req.ProductId,
                        Quantity = req.Quantity,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Cart.Add(cartItem);
                }
                else
                {
                    cartItem.Quantity += req.Quantity;
                }
            }
            else
            {
                var guest = await GetOrCreateGuestAsync(sessionId!);
                cartItem = await _db.Cart.FirstOrDefaultAsync(c => c.GuestId == guest.Id && c.ProductId == req.ProductId);
                int alreadyInCart = cartItem?.Quantity ?? 0;
                if (alreadyInCart + req.Quantity > product.Stock)
                {
                    return BadRequest(new { error = $"Nedostatečný sklad. Na skladě je {product.Stock} ks, v košíku již máte {alreadyInCart} ks." });
                }

                if (cartItem == null)
                {
                    cartItem = new Model.Cart
                    {
                        GuestId = guest.Id,
                        ProductId = req.ProductId,
                        Quantity = req.Quantity,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Cart.Add(cartItem);
                }
                else
                {
                    cartItem.Quantity += req.Quantity;
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Product added successfully" });
        }

        // PUT /api/cart/update
        [HttpPut("update")]
        public async Task<IActionResult> UpdateCart([FromBody] UpdateCartRequest req)
        {
            var (userId, sessionId) = await GetUserOrSessionAsync();
            Model.Cart? cartItem;

            if (userId.HasValue)
            {
                cartItem = await _db.Cart.FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == req.ProductId);
            }
            else
            {
                var guest = await GetOrCreateGuestAsync(sessionId!);
                cartItem = await _db.Cart.FirstOrDefaultAsync(c => c.GuestId == guest.Id && c.ProductId == req.ProductId);
            }

            if (cartItem == null)
            {
                return NotFound(new { error = "Cart item not found" });
            }

            if (req.Quantity <= 0)
            {
                _db.Cart.Remove(cartItem);
            }
            else
            {
                cartItem.Quantity = req.Quantity;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Cart updated successfully" });
        }

        // DELETE /api/cart/remove
        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveFromCart([FromBody] RemoveFromCartRequest req)
        {
            var (userId, sessionId) = await GetUserOrSessionAsync();
            Model.Cart? cartItem;

            if (userId.HasValue)
            {
                cartItem = await _db.Cart.FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == req.ProductId);
            }
            else
            {
                var guest = await GetOrCreateGuestAsync(sessionId!);
                cartItem = await _db.Cart.FirstOrDefaultAsync(c => c.GuestId == guest.Id && c.ProductId == req.ProductId);
            }

            if (cartItem == null)
            {
                return NotFound(new { error = "Cart item not found" });
            }

            _db.Cart.Remove(cartItem);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Product removed from cart" });
        }

        // DELETE /api/cart/clear
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCart()
        {
            var (userId, sessionId) = await GetUserOrSessionAsync();
            List<Model.Cart> cartItems;

            if (userId.HasValue)
            {
                cartItems = await _db.Cart.Where(c => c.UserId == userId.Value).ToListAsync();
            }
            else
            {
                var guest = await GetOrCreateGuestAsync(sessionId!);
                cartItems = await _db.Cart.Where(c => c.GuestId == guest.Id).ToListAsync();
            }

            _db.Cart.RemoveRange(cartItems);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Cart cleared successfully" });
        }
    }
}
