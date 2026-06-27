using Gold_e_Shop.Model;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static Gold_e_Shop.Enums.Enums;

public class AppDbContext : DbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Cart> Cart => Set<Cart>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // categories
        b.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.SortOrder).HasDefaultValue(0);
        });

        // guests
        b.Entity<Guest>(e =>
        {
            e.ToTable("guests");
            e.HasKey(x => x.Id);
            e.Property(x => x.SessionId).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.SessionId).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(100);
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
        });

        // products
        b.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Price).HasPrecision(10, 2).IsRequired();
            e.Property(x => x.Weight).HasPrecision(10, 2);
            e.Property(x => x.Stock).HasDefaultValue(0);
            e.Property(x => x.Likes).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.ImageUrl).HasMaxLength(255);

            e.HasOne(x => x.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(x => x.CategoryId)
             .HasConstraintName("fk_products_category_id");

            e.Property(x => x.SortOrder)
             .HasDefaultValue(0);
        });

        // product_media
        b.Entity<ProductMedia>(e =>
        {
            e.ToTable("product_media");
            e.HasKey(x => x.Id);

            e.Property(x => x.MediaUrl)
             .HasMaxLength(255)
             .IsRequired();

            // ТЕПЕР string
            e.Property(x => x.MediaType)
             .HasMaxLength(20)      // наприклад: "image" | "video"
             .IsRequired();

            e.HasOne(x => x.Product)
             .WithMany(p => p.Media)
             .HasForeignKey(x => x.ProductId)
             .HasConstraintName("fk_product_media_product_id")
             .OnDelete(DeleteBehavior.Cascade);
        });

        // users
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.Password).HasMaxLength(255).IsRequired();
            e.Property(x => x.Email).HasMaxLength(100).IsRequired();

            // ТЕПЕР string
            e.Property(x => x.Role)
             .HasMaxLength(20)
             .HasDefaultValue("user");

            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        // orders
        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(x => x.Id);

            e.Property(x => x.TotalAmount)
             .HasPrecision(10, 2)
             .IsRequired();

            // ТЕПЕР string
            e.Property(x => x.Status)
             .HasMaxLength(20)
             .HasDefaultValue("pending")
             .IsRequired();

            e.Property(x => x.CreatedAt)
             .HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasOne(x => x.User)
             .WithMany(u => u.Orders)
             .HasForeignKey(x => x.UserId)
             .HasConstraintName("fk_orders_user_id");

            e.HasOne(x => x.Guest)
             .WithMany(g => g.Orders)
             .HasForeignKey(x => x.GuestId)
             .HasConstraintName("fk_orders_guest_id");
        });

        // cart
        b.Entity<Cart>(e =>
        {
            e.ToTable("cart");
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasDefaultValue(1);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasOne(x => x.User)
             .WithMany(u => u.Carts)
             .HasForeignKey(x => x.UserId)
             .HasConstraintName("fk_cart_user_id");

            e.HasOne(x => x.Guest)
             .WithMany(g => g.Carts)
             .HasForeignKey(x => x.GuestId)
             .HasConstraintName("fk_cart_guest_id");

            e.HasOne(x => x.Product)
             .WithMany(p => p.Carts)
             .HasForeignKey(x => x.ProductId)
             .IsRequired()
             .HasConstraintName("fk_cart_product_id");
        });

        // favorites
        b.Entity<Favorite>(e =>
        {
            e.ToTable("favorites");
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasOne(x => x.User)
             .WithMany(u => u.Favorites)
             .HasForeignKey(x => x.UserId)
             .HasConstraintName("fk_favorites_user_id");

            e.HasOne(x => x.Guest)
             .WithMany(g => g.Favorites)
             .HasForeignKey(x => x.GuestId)
             .HasConstraintName("fk_favorites_guest_id");

            e.HasOne(x => x.Product)
             .WithMany(p => p.Favorites)
             .HasForeignKey(x => x.ProductId)
             .IsRequired()
             .HasConstraintName("fk_favorites_product_id");
        });

        // order_items
        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(10, 2).IsRequired();

            e.HasOne(x => x.Order)
             .WithMany(o => o.Items)
             .HasForeignKey(x => x.OrderId)
             .IsRequired()
             .HasConstraintName("fk_order_items_order_id");

            e.HasOne(x => x.Product)
             .WithMany(p => p.OrderItems)
             .HasForeignKey(x => x.ProductId)
             .IsRequired()
             .HasConstraintName("fk_order_items_product_id");
        });
    }

}

