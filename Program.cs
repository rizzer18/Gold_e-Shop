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

// ---------- Controllers ----------
app.MapControllers(); // тут підключаються всі контролери (AuthController і т.д.)

// ---------- Тестовий маршрут ----------
app.MapGet("/", () => "✅ Gold eShop API is running...");

app.Run();
