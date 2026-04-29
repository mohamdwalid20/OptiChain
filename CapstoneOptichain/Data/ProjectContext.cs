using Microsoft.EntityFrameworkCore;
using CapstoneOptichain.Models;
namespace CapstoneOptichain.Data
{
	public class ProjectContext : DbContext
	{

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=LEVASCH-STD047\\NEWSQL;Database=CapstoneOptichainOO;Integrated Security=True;Encrypt=False;TrustServerCertificate=True");
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Admin and Manager relationships
            modelBuilder.Entity<ManagerSubscription>()
                .HasOne(ms => ms.Manager)
                .WithOne(m => m.ManagerSubscription)
                .HasForeignKey<ManagerSubscription>(ms => ms.ManagerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure decimal precision
            modelBuilder.Entity<Admin>()
                .Property(a => a.MonthlySubscriptionFee)
                .HasPrecision(10, 2);

            modelBuilder.Entity<ManagerSubscription>()
                .Property(ms => ms.Amount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.OrderValue)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.ProposedPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.BuyingPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SupplierProduct>()
                .Property(sp => sp.BuyingPrice)
                .HasPrecision(10, 2);

            // Manager and Store relationships
            modelBuilder.Entity<Store>()
                .HasOne(s => s.Manager)
                .WithMany(m => m.Stores)
                .HasForeignKey(s => s.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Store and Worker relationships
            modelBuilder.Entity<Worker>()
                .HasOne(w => w.Store)
                .WithMany(s => s.Workers)
                .HasForeignKey(w => w.StoreId)
                .OnDelete(DeleteBehavior.SetNull);

            // Store and Supplier relationships
            modelBuilder.Entity<Supplier>()
                .HasOne(s => s.Store)
                .WithMany(st => st.Suppliers)
                .HasForeignKey(s => s.StoreId)
                .OnDelete(DeleteBehavior.SetNull);

            // Product relationships
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Inventories)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .HasMany(s => s.Inventories)
                .WithOne(i => i.Store)
                .HasForeignKey(i => i.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .HasMany(s => s.Products)
                .WithOne(p => p.Store)
                .HasForeignKey(p => p.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // Order relationships
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // SupplierProposal relationships
            modelBuilder.Entity<SupplierProposal>()
                .HasOne(sp => sp.Order)
                .WithMany()
                .HasForeignKey(sp => sp.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SupplierProposal>()
                .HasOne(sp => sp.Supplier)
                .WithMany()
                .HasForeignKey(sp => sp.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            // ProposalItem relationships
            modelBuilder.Entity<ProposalItem>()
                .HasOne(pi => pi.Proposal)
                .WithMany(sp => sp.ProposalItems)
                .HasForeignKey(pi => pi.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProposalItem>()
                .HasOne(pi => pi.Product)
                .WithMany()
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure decimal precision for new entities
            modelBuilder.Entity<SupplierProposal>()
                .Property(sp => sp.TotalAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<ProposalItem>()
                .Property(pi => pi.UnitPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<ProposalItem>()
                .Property(pi => pi.TotalPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderRequest>()
                .HasOne(or => or.Worker)
                .WithMany()
                .HasForeignKey(or => or.WorkerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderRequest>()
                .HasOne(or => or.Order)
                .WithMany()
                .HasForeignKey(or => or.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderRequest>()
                .HasOne(or => or.Manager)
                .WithMany()
                .HasForeignKey(or => or.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Payments relationships to avoid multiple cascade paths
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Manager)
                .WithMany()
                .HasForeignKey(p => p.ManagerId)
                .OnDelete(DeleteBehavior.NoAction); // prevent multiple cascade path via Manager

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Subscription)
                .WithMany()
                .HasForeignKey(p => p.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed admin data
            modelBuilder.Entity<Admin>().HasData(new Admin
            {
                ID = 1,
                name = "System Admin",
                email = "admin@optichain.com",
                password = "Admin@123", // This should be hashed in production
                CreatedAt = new DateTime(2024, 1, 1),
                IsActive = true,
                MonthlySubscriptionFee = 99.99m,
                MaxManagersAllowed = 1000,
                SystemVersion = "1.0.0"
            });
        }

        public DbSet<Admin> Admins { get; set; }
        public DbSet<Manager> Managers { get; set; }
        public DbSet<ManagerSubscription> ManagerSubscriptions { get; set; }
        public DbSet<Worker> Workers { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Store> Stores { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierProduct> SuppliersProducts { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<OrderRequest> OrderRequests { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<SupportMessage> SupportMessages { get; set; }
        public DbSet<SupplierProposal> SupplierProposals { get; set; }
        public DbSet<ProposalItem> ProposalItems { get; set; }

    }
}
