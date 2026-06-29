// READ-ONLY reference — EF Core's snapshot of the current model, kept in sync
// by `dotnet ef migrations add`. EF diffs THIS against your model to generate
// the next migration. It's the closest thing EF has to a declarative picture of
// the schema — but it's a generated cache, not something you edit.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Shop.Migrations;

[DbContext(typeof(ShopContext))]
partial class ShopContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0");

        modelBuilder.Entity("Shop.Customer", b =>
        {
            b.Property<int>("CustomerId").HasColumnType("int");
            b.Property<string>("Email").IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
            b.Property<string>("FullName").HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.HasKey("CustomerId");
            b.ToTable("Customer");
        });

        modelBuilder.Entity("Shop.Product", b =>
        {
            b.Property<int>("ProductId").HasColumnType("int");
            b.Property<string>("Sku").IsRequired().HasColumnType("varchar(64)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.Property<decimal>("UnitPrice").HasColumnType("decimal(10,2)");
            b.HasKey("ProductId");
            b.ToTable("Product");
        });

        modelBuilder.Entity("Shop.SalesOrder", b =>
        {
            b.Property<int>("OrderId").HasColumnType("int");
            b.Property<int>("CustomerId").HasColumnType("int");
            b.Property<DateTime>("OrderDate").HasColumnType("datetime2");
            b.Property<string>("Status").IsRequired().HasColumnType("varchar(20)");
            b.HasKey("OrderId");
            b.HasIndex("CustomerId");
            b.ToTable("SalesOrder");
        });

        modelBuilder.Entity("Shop.OrderItem", b =>
        {
            b.Property<int>("OrderItemId").HasColumnType("int");
            b.Property<int>("OrderId").HasColumnType("int");
            b.Property<int>("ProductId").HasColumnType("int");
            b.Property<int>("Quantity").HasColumnType("int");
            b.Property<decimal>("UnitPrice").HasColumnType("decimal(10,2)");
            b.HasKey("OrderItemId");
            b.HasIndex("OrderId");
            b.HasIndex("ProductId");
            b.ToTable("OrderItem");
        });

        modelBuilder.Entity("Shop.SalesOrder", b =>
            b.HasOne("Shop.Customer", "Customer").WithMany()
                .HasForeignKey("CustomerId").OnDelete(DeleteBehavior.Cascade).IsRequired());

        modelBuilder.Entity("Shop.OrderItem", b =>
        {
            b.HasOne("Shop.SalesOrder", "Order").WithMany()
                .HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.HasOne("Shop.Product", "Product").WithMany()
                .HasForeignKey("ProductId").OnDelete(DeleteBehavior.Cascade).IsRequired();
        });
    }
}
