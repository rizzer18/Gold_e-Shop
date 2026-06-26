using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using System.Text;
using static Gold_e_Shop.Enums.Enums;
using Gold_e_Shop.Services;
using Microsoft.Extensions.FileProviders;
using System.IO;


var builder = WebApplication.CreateBuilder(args);

// ---------- DATABASE ----------
var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;
var dsb = new NpgsqlDataSourceBuilder(cs);
var dataSource = dsb.Build();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(dataSource));

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ---------- JWT AUTH ----------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
var key = Encoding.UTF8.GetBytes(jwt.Secret);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // true на проді!
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ---------- CONTROLLERS ----------
builder.Services.AddControllers();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                     ?? new[] { "http://localhost:3000", "https://zlaty-eshop-project-production.up.railway.app" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendOnly", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // ⬅️ дозвіл на кукі / credentials
    });
});

// ---------- SWAGGER ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Gold eShop API",
        Version = "v1",
        Description = "REST API for Gold eShop backend"
    });

    // JWT підтримка в Swagger
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введи: Bearer {token}"
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// ===========================================
// 2) Побудова застосунку
// ===========================================
var app = builder.Build();

// Automatically apply database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            Console.WriteLine("⏳ Applying pending database migrations...");
            context.Database.Migrate();
            Console.WriteLine("✅ Database migrations applied successfully.");
        }
        else
        {
            Console.WriteLine("✅ Database is up to date. No pending migrations.");
        }

        // Seed default admin user if no users exist
        if (!context.Users.Any())
        {
            Console.WriteLine("⏳ Seeding default admin user...");
            var adminUser = new Gold_e_Shop.Model.User
            {
                Username = "admin",
                Email = "admin@example.com",
                Password = BCrypt.Net.BCrypt.HashPassword("AdminPassword123!"),
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(adminUser);
            context.SaveChanges();
            Console.WriteLine("✅ Seeded admin: admin@example.com / AdminPassword123!");
        }

        // Seed default products if database is empty
        if (!context.Products.Any())
        {
            Console.WriteLine("⏳ Seeding default products...");
            
            // Create a default category
            var category = context.Categories.FirstOrDefault();
            if (category == null)
            {
                category = new Gold_e_Shop.Model.Category { Name = "Talismany" };
                context.Categories.Add(category);
                context.SaveChanges();
            }

            var product1 = new Gold_e_Shop.Model.Product
            {
                Name = "Talisman Síla Týmu",
                Description = "Limitovaný stříbrný talisman zobrazující jednotu, vytrvalost a odhodlání šampiónů.",
                Price = 2490.00m,
                CategoryId = category.Id,
                Stock = 10,
                CreatedAt = DateTime.UtcNow,
                Likes = 0,
                Specification = "Ruční výroba, rytina",
                Material = "Stříbro 925/1000",
                Weight = 12.50m
            };

            var product2 = new Gold_e_Shop.Model.Product
            {
                Name = "Zlatý prsten Bojovník",
                Description = "Elegance spojená se skrytou vnitřní silou. Perfektní doplněk pro každodenní motivaci.",
                Price = 8990.00m,
                CategoryId = category.Id,
                Stock = 5,
                CreatedAt = DateTime.UtcNow,
                Likes = 0,
                Specification = "Šířka 6mm, leštěný povrch",
                Material = "Žluté zlato 585/1000",
                Weight = 6.80m
            };

            var product3 = new Gold_e_Shop.Model.Product
            {
                Name = "Stříbrný přívěsek Šampión",
                Description = "Masivní přívěsek inspirovaný disciplínou a vítězstvím v ringu. Dodáváno s koženou šňůrkou.",
                Price = 3190.00m,
                CategoryId = category.Id,
                Stock = 8,
                CreatedAt = DateTime.UtcNow,
                Likes = 0,
                Specification = "Rozměry 25x25mm",
                Material = "Patina stříbro 925/1000",
                Weight = 9.20m
            };

            context.Products.AddRange(product1, product2, product3);
            context.SaveChanges();

            // Add media for the products
            var media1 = new Gold_e_Shop.Model.ProductMedia
            {
                ProductId = product1.Id,
                MediaUrl = "/uploads/media__1782490079650.jpg",
                MediaType = "image"
            };

            var media2 = new Gold_e_Shop.Model.ProductMedia
            {
                ProductId = product2.Id,
                MediaUrl = "/uploads/media__1782490079650.jpg",
                MediaType = "image"
            };

            var media3 = new Gold_e_Shop.Model.ProductMedia
            {
                ProductId = product3.Id,
                MediaUrl = "/uploads/media__1782490079650.jpg",
                MediaType = "image"
            };

            context.ProductMedia.AddRange(media1, media2, media3);
            context.SaveChanges();
            
            Console.WriteLine("✅ Seeded 3 default products with boxer image.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ An error occurred while migrating the database: {ex.Message}");
    }
}
app.UseStaticFiles();
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads" 
});


// ---------- Swagger ----------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gold eShop API v1");
    c.RoutePrefix = "swagger"; // доступ: http://localhost:xxxx/swagger
});

// ---------- Middleware ----------
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseCors("FrontendOnly");

// ---------- Controllers ----------
app.MapControllers(); // тут підключаються всі контролери (AuthController і т.д.)

// ---------- Тестовий маршрут ----------
app.MapGet("/", () => "✅ Gold eShop API is running...");

app.Run();
