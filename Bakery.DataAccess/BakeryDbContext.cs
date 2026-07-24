using Microsoft.EntityFrameworkCore;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;

namespace Bakery.DataAccess
{
    public class BakeryDbContext : DbContext
    {
        public BakeryDbContext(DbContextOptions<BakeryDbContext> options) : base(options)
        {
        }

        public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
        public DbSet<MeasurementUnit> MeasurementUnits { get; set; } = null!;
        public DbSet<RawMaterial> RawMaterials { get; set; } = null!;
        public DbSet<ProductionRecipe> ProductionRecipes { get; set; } = null!;
        public DbSet<ProductionRecipeItem> ProductionRecipeItems { get; set; } = null!;
        public DbSet<ProductionSetting> ProductionSettings { get; set; } = null!;
        public DbSet<ProductionOrder> ProductionOrders { get; set; } = null!;
        public DbSet<ProductionOrderResult> ProductionOrderResults { get; set; } = null!;
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<TreasuryTransaction> TreasuryTransactions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Expense Category
            modelBuilder.Entity<ExpenseCategory>()
                .Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();

            // Expense
            modelBuilder.Entity<Expense>()
                .Property(e => e.Quantity).HasPrecision(18, 3);
            modelBuilder.Entity<Expense>()
                .Property(e => e.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Expense>()
                .Property(e => e.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Expense>()
                .Property(e => e.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Expense>()
                .Property(e => e.RemainingAmount).HasPrecision(18, 2);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.ExpenseCategory)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Employee)
                .WithMany(emp => emp.Expenses)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Measurement Unit
            modelBuilder.Entity<MeasurementUnit>()
                .Property(u => u.Name)
                .HasMaxLength(50)
                .IsRequired();

            // Raw Material
            modelBuilder.Entity<RawMaterial>()
                .Property(r => r.Name)
                .HasMaxLength(150)
                .IsRequired();
            modelBuilder.Entity<RawMaterial>()
                .Property(r => r.CurrentQuantity).HasPrecision(18, 3);
            modelBuilder.Entity<RawMaterial>()
                .Property(r => r.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<RawMaterial>()
                .Property(r => r.TotalValue).HasPrecision(18, 2);

            modelBuilder.Entity<RawMaterial>()
                .HasOne(r => r.MeasurementUnit)
                .WithMany(u => u.RawMaterials)
                .HasForeignKey(r => r.MeasurementUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // Production Recipe & Items
            modelBuilder.Entity<ProductionRecipe>()
                .Property(p => p.FlourSackQuantity).HasPrecision(18, 2);

            modelBuilder.Entity<ProductionRecipeItem>()
                .Property(i => i.RequiredQuantity).HasPrecision(18, 3);

            modelBuilder.Entity<ProductionRecipeItem>()
                .HasOne(i => i.ProductionRecipe)
                .WithMany(r => r.RecipeItems)
                .HasForeignKey(i => i.ProductionRecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductionRecipeItem>()
                .HasOne(i => i.RawMaterial)
                .WithMany(r => r.RecipeItems)
                .HasForeignKey(i => i.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            // Production Setting
            modelBuilder.Entity<ProductionSetting>()
                .Property(s => s.MabroumPiecesPerSack).HasPrecision(18, 2);
            modelBuilder.Entity<ProductionSetting>()
                .Property(s => s.PanePiecesPerSack).HasPrecision(18, 2);
            modelBuilder.Entity<ProductionSetting>()
                .Property(s => s.SandwichPiecesPerSack).HasPrecision(18, 2);
            modelBuilder.Entity<ProductionSetting>()
                .Property(s => s.BasketSellingPrice).HasPrecision(18, 2);

            // Production Order & Results
            modelBuilder.Entity<ProductionOrder>()
                .Property(o => o.FlourSackCount).HasPrecision(18, 2);
            modelBuilder.Entity<ProductionOrder>()
                .Property(o => o.BasketSellingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<ProductionOrder>()
                .Property(o => o.TotalExpectedSalesValue).HasPrecision(18, 2);
            modelBuilder.Entity<ProductionOrder>()
                .Property(o => o.TotalActualSalesValue).HasPrecision(18, 2);

            modelBuilder.Entity<ProductionOrderResult>()
                .Property(r => r.AchievementPercentage).HasPrecision(18, 2);

            modelBuilder.Entity<ProductionOrderResult>()
                .HasOne(r => r.ProductionOrder)
                .WithMany(o => o.OrderResults)
                .HasForeignKey(r => r.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Inventory Transaction
            modelBuilder.Entity<InventoryTransaction>()
                .Property(t => t.Quantity).HasPrecision(18, 3);
            modelBuilder.Entity<InventoryTransaction>()
                .Property(t => t.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<InventoryTransaction>()
                .Property(t => t.TotalAmount).HasPrecision(18, 2);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.RawMaterial)
                .WithMany(r => r.InventoryTransactions)
                .HasForeignKey(t => t.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.ProductionOrder)
                .WithMany(o => o.InventoryTransactions)
                .HasForeignKey(t => t.ProductionOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Expense)
                .WithMany()
                .HasForeignKey(t => t.ExpenseId)
                .OnDelete(DeleteBehavior.SetNull);

            // Employee
            modelBuilder.Entity<Employee>()
                .Property(e => e.MonthlySalary).HasPrecision(18, 2);

            // Treasury Transaction
            modelBuilder.Entity<TreasuryTransaction>()
                .Property(t => t.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<TreasuryTransaction>()
                .Property(t => t.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<TreasuryTransaction>()
                .Property(t => t.RemainingAmount).HasPrecision(18, 2);

            // Seed Lookup Data
            modelBuilder.Entity<ExpenseCategory>().HasData(
                new ExpenseCategory { Id = 1, Name = "عمالة", IsSystem = true },
                new ExpenseCategory { Id = 2, Name = "مواد خام", IsSystem = true },
                new ExpenseCategory { Id = 3, Name = "مصاريف تشغيل (كهرباء، غاز، مياه، نقل، صيانة)", IsSystem = true }
            );

            modelBuilder.Entity<MeasurementUnit>().HasData(
                new MeasurementUnit { Id = 1, Name = "كجم" },
                new MeasurementUnit { Id = 2, Name = "جرام" },
                new MeasurementUnit { Id = 3, Name = "شكارة" },
                new MeasurementUnit { Id = 4, Name = "قطعة" },
                new MeasurementUnit { Id = 5, Name = "لتر" },
                new MeasurementUnit { Id = 6, Name = "كرتونة" }
            );

            modelBuilder.Entity<ProductionSetting>().HasData(
                new ProductionSetting
                {
                    Id = 1,
                    MabroumPiecesPerSack = 56,
                    PanePiecesPerSack = 43,
                    SandwichPiecesPerSack = 51,
                    PiecesPerBasket = 28,
                    BasketSellingPrice = 100,
                    LastUpdatedDate = new DateTime(2026, 1, 1)
                }
            );

            modelBuilder.Entity<ProductionRecipe>().HasData(
                new ProductionRecipe
                {
                    Id = 1,
                    Name = "الوصفة القياسية لشكارة الدقيق",
                    FlourSackQuantity = 1,
                    IsActive = true
                }
            );
        }
    }
}
