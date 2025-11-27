using System.Net;
using System.Net.Mail;
using System.Text;
using Gold_e_Shop.DTO;
using Gold_e_Shop.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly string _gmailUser;
    private readonly string _gmailAppPassword;

    public OrdersController(AppDbContext db, IConfiguration config)
    {
        _db = db;

        _gmailUser = config["Gmail:User"]
                     ?? Environment.GetEnvironmentVariable("GMAIL_USER")
                     ?? throw new InvalidOperationException("Gmail user not configured");

        _gmailAppPassword = config["Gmail:AppPassword"]
                            ?? Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD")
                            ?? throw new InvalidOperationException("Gmail app password not configured");
    }
    [Authorize(Roles = "admin")]
    [HttpGet]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _db.Orders
            .Include(o => o.Guest)
            .Include(o => o.Items)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Media)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(o => new
        {
            orderId = o.Id,
            total_amount = o.TotalAmount,
            status = o.Status,
            created_at = o.CreatedAt,

            guest = o.Guest == null ? null : new
            {
                id = o.Guest.Id,
                name = o.Guest.Name,
                surname = o.Guest.Surname,
                email = o.Guest.Email,
                phone = o.Guest.PhoneNumber,
                address = new
                {
                    street = o.Guest.Street,
                    city = o.Guest.City,
                    houseNumber = o.Guest.HouseNumber,
                    country = o.Guest.Country
                }
            },

            // Масив товарів
            items = o.Items.Select(oi => new
            {
                productId = oi.ProductId,
                name = oi.Product!.Name,
                description = oi.Product.Description,
                // у Node було p.price – тут можеш взяти або ціну з продукту, або з order_item
                price = oi.Price,
                quantity = oi.Quantity,
                category_id = oi.Product.CategoryId,
                specification = oi.Product.Specification,
                material = oi.Product.Material,
                weight = oi.Product.Weight,
                mediaUrls = (oi.Product.Media ?? new List<ProductMedia>())
                    .Select(m => ToPublicUrl(m.MediaUrl))
                    .ToArray()
            })
        });

        return Ok(new { orders = result });
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
    [HttpPost]
    public async Task<IActionResult> CreateOrderForGuest([FromBody] CreateOrderForGuestRequest req)
    {
        var guestInfo = req.GuestInfo;
        var cartItems = req.CartItems;

        if (guestInfo == null ||
            cartItems == null ||
            cartItems.Count == 0 ||
            string.IsNullOrWhiteSpace(guestInfo.Name) ||
            string.IsNullOrWhiteSpace(guestInfo.Surname) ||
            string.IsNullOrWhiteSpace(guestInfo.Email) ||
            string.IsNullOrWhiteSpace(guestInfo.Phone) ||
            guestInfo.Address == null ||
            string.IsNullOrWhiteSpace(guestInfo.Address.Street) ||
            string.IsNullOrWhiteSpace(guestInfo.Address.City) ||
            string.IsNullOrWhiteSpace(guestInfo.Address.HouseNumber) ||
            string.IsNullOrWhiteSpace(guestInfo.Address.Country))
        {
            return BadRequest(new
            {
                error = "Všechna pole osobních údajů, adresy a položky košíku jsou povinné."
            });
        }

        var name = guestInfo.Name;
        var surname = guestInfo.Surname;
        var email = guestInfo.Email;
        var phone = guestInfo.Phone;
        var street = guestInfo.Address.Street;
        var city = guestInfo.Address.City;
        var houseNumber = guestInfo.Address.HouseNumber;
        var country = guestInfo.Address.Country;

        var sessionId = Guid.NewGuid().ToString();

        try
        {
            // IDs produktů z košíku
            var productIds = cartItems.Select(i => i.ProductId).ToList();

            // Získat produkty z DB
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Price, p.Name })
                .ToListAsync();

            var priceMap = products.ToDictionary(p => p.Id, p => p.Price);
            var nameMap = products.ToDictionary(p => p.Id, p => p.Name);

            // Kontrola, že všechny produkty existují
            foreach (var item in cartItems)
            {
                if (!priceMap.ContainsKey(item.ProductId))
                {
                    return BadRequest(new
                    {
                        error = $"Produkt s ID {item.ProductId} nenalezen."
                    });
                }
            }

            // Výpočet celkové částky
            decimal totalAmount = cartItems.Sum(item =>
                priceMap[item.ProductId] * item.Quantity);

            // --- Uložení guest-a ---
            var guest = new Guest
            {
                SessionId = sessionId,
                Name = name,
                Surname = surname,
                Email = email,
                PhoneNumber = phone,
                Street = street,
                City = city,
                HouseNumber = houseNumber,
                Country = country,
                CreatedAt = DateTime.UtcNow
            };

            _db.Guests.Add(guest);
            await _db.SaveChangesAsync();

            var guestId = guest.Id;

            // --- Uložení objednávky ---
            var order = new Order
            {
                GuestId = guestId,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            var orderId = order.Id;

            // --- Uložení položek objednávky ---
            foreach (var item in cartItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = orderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = priceMap[item.ProductId]
                };
                _db.OrderItems.Add(orderItem);
            }

            await _db.SaveChangesAsync();

            // --- HTML pro řádky tabulky ---
            var itemsHtml = new StringBuilder();
            foreach (var i in cartItems)
            {
                var productName = nameMap[i.ProductId];
                var price = priceMap[i.ProductId];
                var lineTotal = price * i.Quantity;

                itemsHtml.Append($@"
      <tr>
        <td style=""border:1px solid #ddd;"">{productName}</td>
        <td style=""border:1px solid #ddd; text-align:right;"">{i.Quantity}</td>
        <td style=""border:1px solid #ddd; text-align:right;"">{price:F2} Kč</td>
        <td style=""border:1px solid #ddd; text-align:right;"">{lineTotal:F2} Kč</td>
      </tr>");
            }

            // --- Email pro zákazníka ---
            var guestHtml = $@"
<!DOCTYPE html>
<html lang=""cs"">
<head>
  <meta charset=""UTF-8"">
  <title>Potvrzení objednávky</title>
</head>
<body style=""font-family:Arial, sans-serif; color:#333; line-height:1.4; padding:20px;"">
  <h1 style=""color:#005f8b; border-bottom:2px solid #005f8b; padding-bottom:5px;"">
    Děkujeme za Vaši objednávku!
  </h1>

  <h2 style=""margin-top:20px; color:#005f8b;"">Informace o objednávce</h2>
  <p><strong>Číslo objednávky:</strong> {orderId}</p>

  <table width=""100%"" cellpadding=""8"" cellspacing=""0"" style=""border-collapse:collapse; margin:20px 0;"">
    <thead>
      <tr style=""background:#f0f0f0;"">
        <th style=""border:1px solid #ddd; text-align:left;"">Produkt</th>
        <th style=""border:1px solid #ddd; text-align:right;"">Množství</th>
        <th style=""border:1px solid #ddd; text-align:right;"">Cena za ks</th>
        <th style=""border:1px solid #ddd; text-align:right;"">Celkem</th>
      </tr>
    </thead>
    <tbody>
      {itemsHtml}
    </tbody>
    <tfoot>
      <tr>
        <td colspan=""3"" style=""border:1px solid #ddd; text-align:right;""><strong>Celková částka:</strong></td>
        <td style=""border:1px solid #ddd; text-align:right;""><strong>{totalAmount:F2} Kč</strong></td>
      </tr>
    </tfoot>
  </table>

  <h2 style=""color:#005f8b;"">Dodací údaje</h2>
  <p>
    {street}, č.p. {houseNumber}<br>
    {city}, {country}<br>
    Telefon: {phone}<br>
    Email: {email}
  </p>

  <p style=""font-size:0.9em; color:#666; margin-top:30px;"">
    Session ID: {sessionId}<br>
    V případě dotazů nás kontaktujte na <a href=""mailto:{_gmailUser}"">{_gmailUser}</a>.
  </p>
</body>
</html>
";

            // --- Email pro admina ---
            var adminHtml = $@"
<!DOCTYPE html>
<html lang=""cs"">
<head>
  <meta charset=""UTF-8"">
  <title>Nová objednávka</title>
</head>
<body style=""font-family:Arial, sans-serif; color:#333; line-height:1.4; padding:20px;"">
  <h1 style=""color:#8b0000; border-bottom:2px solid #8b0000; padding-bottom:5px;"">
    Nová objednávka č. {orderId}
  </h1>

  <h2 style=""margin-top:20px; color:#8b0000;"">Objednávka od zákazníka</h2>
  <p>
    <strong>Jméno:</strong> {name} {surname}<br>
    <strong>Email:</strong> <a href=""mailto:{email}"">{email}</a><br>
    <strong>Telefon:</strong> {phone}
  </p>

  <h2 style=""color:#8b0000;"">Seznam produktů</h2>
  <table width=""100%"" cellpadding=""8"" cellspacing=""0"" style=""border-collapse:collapse; margin:20px 0;"">
    <thead>
      <tr style=""background:#f9e6e6;"">
        <th style=""border:1px solid #ddd; text-align:left;"">Produkt</th>
        <th style=""border:1px solid #ddd; text-align:right;"">Množství</th>
        <th style=""border:1px solid #ddd; text-align:right;"">Cena za ks</th>
        <th style=""border:1px solid #ddd; text-align:right;"">Celkem</th>
      </tr>
    </thead>
    <tbody>
      {itemsHtml}
    </tbody>
    <tfoot>
      <tr>
        <td colspan=""3"" style=""border:1px solid #ddd; text-align:right;""><strong>Celková částka:</strong></td>
        <td style=""border:1px solid #ddd; text-align:right;""><strong>{totalAmount:F2} Kč</strong></td>
      </tr>
    </tfoot>
  </table>

  <h2 style=""color:#8b0000;"">Dodací adresa</h2>
  <p>{street}, č.p. {houseNumber}, {city}, {country}</p>

  <p style=""font-size:0.9em; color:#666; margin-top:30px;"">
    Session ID: {sessionId}
  </p>
</body>
</html>
";

            // --- Odeslání emailů (Gmail SMTP) ---
            await Task.WhenAll(
                SendEmailAsync(email, $"Potvrzení objednávky č. {orderId}", guestHtml),
                SendEmailAsync(_gmailUser, $"Nová objednávka č. {orderId}", adminHtml)
            );

            return StatusCode(200, new
            {
                message = "Objednávka vytvořena a e-maily odeslány",
                orderId,
                sessionId
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in CreateOrderForGuest: {ex}");
            return StatusCode(500, new
            {
                error = "Nepodařilo se vytvořit objednávku nebo odeslat e-mail"
            });
        }
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            Credentials = new NetworkCredential(_gmailUser, _gmailAppPassword),
            EnableSsl = true
        };

        var mail = new MailMessage
        {
            From = new MailAddress(_gmailUser, "Zlatý E-shop"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(to);

        await client.SendMailAsync(mail);
    }
    [Authorize(Roles = "admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOrder(int id, [FromBody] UpdateOrderDto dto)
    {
        // як у Node: мусить бути хоча б одне поле
        if (string.IsNullOrWhiteSpace(dto.Status) && dto.Total_Amount == null)
        {
            return BadRequest(new
            {
                error = "Musíte zadat alespoň jedno pole k aktualizaci: status nebo total_amount."
            });
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound(new { error = "Objednávka nenalezena." });
        }

        // часткове оновлення – тільки те, що прийшло
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            order.Status = dto.Status;
        }

        if (dto.Total_Amount != null)
        {
            order.TotalAmount = dto.Total_Amount.Value;
        }

        await _db.SaveChangesAsync();

        // форма відповіді як у Node:
        // RETURNING id, guest_id AS "guestId", total_amount, status, created_at
        var result = new
        {
            id = order.Id,
            guestId = order.GuestId,
            total_amount = order.TotalAmount,
            status = order.Status,
            created_at = order.CreatedAt
        };

        return Ok(new { order = result });
    }
    [Authorize(Roles = "admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { error = "Objednávka nenalezena." });
        }

        // Видаляємо всі order_items
        if (order.Items != null && order.Items.Count > 0)
        {
            _db.OrderItems.RemoveRange(order.Items);
        }

        // Видаляємо саму objednávku
        _db.Orders.Remove(order);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Objednávka č. {id} byla úspěšně smazána."
        });
    }


}
