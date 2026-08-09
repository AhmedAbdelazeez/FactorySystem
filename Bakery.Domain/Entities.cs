using System;
using System.Collections.Generic;
using Bakery.Domain.Enums;

namespace Bakery.Domain.Entities
{
    public class ExpenseCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsSystem { get; set; } = false;

        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }

    public class Expense
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ExpenseCategoryId { get; set; }
        public ExpenseCategory? ExpenseCategory { get; set; }

        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? Notes { get; set; }

        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }
    }

    public class MeasurementUnit
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<RawMaterial> RawMaterials { get; set; } = new List<RawMaterial>();
    }

    public class MaterialType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<RawMaterial> RawMaterials{ get; set; }= new List<RawMaterial>();
    }

    public class RawMaterial
    {
        public int Id { get; set; }

        public int MaterialTypeId { get; set; }
        
        public MaterialType? MaterialType { get; set; }
        
        
        public decimal CurrentQuantity { get; set; }
        public int MeasurementUnitId { get; set; }
        public MeasurementUnit? MeasurementUnit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime LastUpdatedDate { get; set; } = DateTime.Now;

        public ICollection<ProductionRecipeItem> RecipeItems { get; set; } = new List<ProductionRecipeItem>();
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }

    public class ProductionRecipe
    {
        public int Id { get; set; }
        public string Name { get; set; } = "وصفة شكارة الدقيق";
        public decimal FlourSackQuantity { get; set; } = 1;
        public bool IsActive { get; set; } = true;

        public ICollection<ProductionRecipeItem> RecipeItems { get; set; } = new List<ProductionRecipeItem>();
    }

    public class ProductionRecipeItem
    {
        public int Id { get; set; }
        public int ProductionRecipeId { get; set; }
        public ProductionRecipe? ProductionRecipe { get; set; }

        public int RawMaterialId { get; set; }
        public RawMaterial? RawMaterial { get; set; }

        public decimal RequiredQuantity { get; set; } // الكمية المطلوبة لكل شكارة دقيق واحدة
    }

    public class ProductionSetting
    {
        public int Id { get; set; }
        public decimal MabroumPiecesPerSack { get; set; } = 56;
        public decimal PanePiecesPerSack { get; set; } = 43;
        public decimal SandwichPiecesPerSack { get; set; } = 51;
        public int PiecesPerBasket { get; set; } = 28;
        public decimal BasketSellingPrice { get; set; } = 100; // سعر بيع الباسكيت الافتراضي
        public DateTime LastUpdatedDate { get; set; } = DateTime.Now;
    }

    public class ProductionOrder
    {
        public int Id { get; set; }
        public DateTime ProductionDate { get; set; } = DateTime.Today;
        public decimal FlourSackCount { get; set; }
        public ProductType SelectedProductType { get; set; }
        public decimal BasketSellingPrice { get; set; }
        public ProductionStatus Status { get; set; } = ProductionStatus.Draft;

        public int TotalTargetPieces { get; set; }
        public int TotalActualPieces { get; set; }
        public int ExpectedBaskets { get; set; }
        public int ActualBaskets { get; set; }
        public int RemainingPieces { get; set; }
        public decimal TotalExpectedSalesValue { get; set; }
        public decimal TotalActualSalesValue { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ConfirmedAt { get; set; }

        public ICollection<ProductionOrderResult> OrderResults { get; set; } = new List<ProductionOrderResult>();
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

        public  ICollection<TreasuryTransaction> TreasuryTransactions { get; set; } = new List<TreasuryTransaction>();
    
    }

    public class ProductionOrderResult
    {
        public int Id { get; set; }
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        public ProductType ProductType { get; set; }
        public string ProductTypeName { get; set; } = string.Empty;
        public int TargetQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public int Difference { get; set; }
        public decimal AchievementPercentage { get; set; }
    }

    public class InventoryTransaction
    {
        public int Id { get; set; }
        public int RawMaterialId { get; set; }
        public RawMaterial? RawMaterial { get; set; }

        public TransactionType TransactionType { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public int? ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        public int? ExpenseId { get; set; }
        public Expense? Expense { get; set; }

        public string? Notes { get; set; }
    }

    public class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public decimal MonthlySalary { get; set; }
        public string WeeklyDayOff { get; set; } = "الجمعة";
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime StartedDate { get; set; } = DateTime.Now;

        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public ICollection<EmployeeAttendance> Attendances { get; set; } = new List<EmployeeAttendance>();

        public ICollection<EmployeeAdvance> Advances { get; set; } = new List<EmployeeAdvance>();
    }

    public class EmployeeAttendance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public bool IsPresent { get; set; } = true;
        public DateTime? CheckInTime { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class EmployeeAdvance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public decimal Amount { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public PaymentMethod PaymentMethod { get; set; }
        public string? Notes { get; set; }

        public bool IsPaid { get; set; } = false;

        public DateTime? PaidDate { get; set; }
    }

    public class TreasuryTransaction
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string TransactionName { get; set; } = string.Empty;
        public TreasuryTransactionType TransactionType { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? Notes { get; set; }

        public decimal? SoldBaskets { get; set; }  // جديد - عشان نعرف بالظبط كام باسكيت اتباع في الحركة دي
        public int? ExpenseId { get; set; }
        public int? ProductionOrderId { get; set; }
        public  ProductionOrder? ProductionOrder { get; set; }
    }
}
