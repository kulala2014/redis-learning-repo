using Microsoft.EntityFrameworkCore;
using RedisApi.Models;

namespace RedisApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Seed data
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Laptop", Price = 999.99m, Description = "High performance laptop" },
            new Product { Id = 2, Name = "Mouse", Price = 29.99m, Description = "Wireless mouse" },
            new Product { Id = 3, Name = "Keyboard", Price = 59.99m, Description = "Mechanical keyboard" }
        );
    }
}
