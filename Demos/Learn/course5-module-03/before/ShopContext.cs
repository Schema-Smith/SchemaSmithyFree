// READ-ONLY reference. This is the EF Core model that built the shop — the C#
// your migrations were generated from. You do NOT run EF in this lab; the
// course5-setup script already applied this model's end state (the four tables
// plus __EFMigrationsHistory) to shop_from_efcore. It's here so you can see the
// schema that lived in code.
using Microsoft.EntityFrameworkCore;

namespace Shop;

public class ShopContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlServer(
            "Server=localhost,11433;Database=shop_from_efcore;User Id=sa;Password=Learn!Passw0rd;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>(e =>
        {
            e.HasKey(x => x.CustomerId);
            e.Property(x => x.CustomerId).ValueGeneratedNever();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200);
        });
        b.Entity<Product>(e =>
        {
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).ValueGeneratedNever();
            e.Property(x => x.Sku).HasColumnType("varchar(64)").IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.UnitPrice).HasColumnType("decimal(10,2)");
        });
        b.Entity<SalesOrder>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).ValueGeneratedNever();
            e.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        });
        b.Entity<OrderItem>(e =>
        {
            e.HasKey(x => x.OrderItemId);
            e.Property(x => x.OrderItemId).ValueGeneratedNever();
            e.Property(x => x.UnitPrice).HasColumnType("decimal(10,2)");
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });
    }
}

public class Customer { public int CustomerId { get; set; } public string Email { get; set; } = ""; public string? FullName { get; set; } }
public class Product { public int ProductId { get; set; } public string Sku { get; set; } = ""; public string Name { get; set; } = ""; public decimal UnitPrice { get; set; } }
public class SalesOrder { public int OrderId { get; set; } public int CustomerId { get; set; } public Customer Customer { get; set; } = null!; public DateTime OrderDate { get; set; } public string Status { get; set; } = ""; }
public class OrderItem { public int OrderItemId { get; set; } public int OrderId { get; set; } public SalesOrder Order { get; set; } = null!; public int ProductId { get; set; } public Product Product { get; set; } = null!; public int Quantity { get; set; } public decimal UnitPrice { get; set; } }
