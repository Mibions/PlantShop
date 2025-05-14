using Microsoft.EntityFrameworkCore;
using PlantShop.Models;

namespace PlantShop.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Plant> Plants { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure decimal precision
            modelBuilder.Entity<Plant>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderDetail>()
                .Property(od => od.UnitPrice)
                .HasPrecision(18, 2);

            // Cấu hình mối quan hệ giữa Order và OrderDetail
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.OrderId);

            // Cấu hình mối quan hệ giữa OrderDetail và Plant
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Plant)
                .WithMany()
                .HasForeignKey(od => od.PlantId);

            // Cấu hình mối quan hệ giữa Order và User
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Cấu hình mối quan hệ giữa Plant và Category
            modelBuilder.Entity<Plant>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Plants)
                .HasForeignKey(p => p.CategoryId);
        }
    }
}