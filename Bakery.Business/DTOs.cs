using System;
using System.Collections.Generic;
using Bakery.Domain.Enums;

namespace Bakery.Business.DTOs
{
    public class ProductionCalculationResultDto
    {
        public decimal FlourSackCount { get; set; }
        public ProductType ProductType { get; set; }           // ← جديد
        public string ProductTypeName { get; set; } = "";      // ← جديد
        public int TargetQuantity { get; set; }                // ← جديد
        public int TotalTargetPieces { get; set; }
        public decimal ExpectedBaskets { get; set; }
        public int RemainingPieces { get; set; }
        public decimal BasketSellingPrice { get; set; }
        public decimal TotalExpectedSalesValue { get; set; }
        public List<RequiredMaterialDto> RequiredMaterials { get; set; } = new();
    }

    public class RequiredMaterialDto
    {
        public int RawMaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string MeasurementUnitName { get; set; } = string.Empty;
        public decimal RequiredQuantityPerSack { get; set; }
        public decimal TotalRequiredQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public bool IsSufficient => AvailableQuantity >= TotalRequiredQuantity;
        public decimal Shortage => IsSufficient ? 0 : (TotalRequiredQuantity - AvailableQuantity);
    }

    public class ProductionOrderDto
    {
        public int Id { get; set; }
        public DateTime ProductionDate { get; set; }
        public decimal FlourSackCount { get; set; }
        public ProductType SelectedProductType { get; set; }         // ← جديد
        public string SelectedProductTypeName { get; set; } = "";    // ← جديد
        public decimal BasketSellingPrice { get; set; }
        public ProductionStatus Status { get; set; }
        public string StatusName => Status == ProductionStatus.Confirmed ? "مؤكد" : "مسودة";
        public int TotalTargetPieces { get; set; }
        public int TotalActualPieces { get; set; }
        public decimal ExpectedBaskets { get; set; }
        public decimal ActualBaskets { get; set; }
        public int RemainingPieces { get; set; }
        public decimal TotalExpectedSalesValue { get; set; }
        public decimal TotalActualSalesValue { get; set; }
        public decimal SoldBaskets { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public List<ProductionOrderResultDto> OrderResults { get; set; } = new();
    }


    public class ProductOrderSalesHistoryDto
    {
        public int Id { get; set; }               // رقم سجل البيع
        public int ProductionOrderId { get; set; }           // رقم أمر الإنتاج المرتبط بالبيع
        public DateTime ProductSoldAt { get; set; }         // تاريخ البيع
        
        
        public ProductType ProductType { get; set; }
        public string SelectedProductTypeName { get; set; } = "";


        public decimal FlourSackCount { get; set; }
        public decimal BasketSellingPrice { get; set; }

        public int TotalActualPieces { get; set; }
        public decimal TotalActualBaskets { get; set; }
        public int RemainingPieces { get; set; }
        public decimal RemainingBasketsInOrder { get; set; } // المتبقي
        
        public decimal SoldBaskets { get; set; }
        public decimal TotalAmount => SoldBaskets * BasketSellingPrice;
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount => TotalAmount - PaidAmount;
        public PaymentMethod PaymentMethod { get; set; }

        public decimal Income => PaidAmount;
        public string? Notes { get; set; }
    }

    public class CreateProductSaleDto
    {
        public int ProductionOrderId { get; set; }               // أمر الإنتاج المراد البيع منه
        public decimal SoldBaskets { get; set; }                 // عدد الباسكيت المباع
        public decimal BasketSellingPrice { get; set; }          // سعر الباسكيت
        public PaymentMethod PaymentMethod { get; set; }         // طريقة الدفع
        public decimal PaidAmount { get; set; }                  // المبلغ المدفوع
                      
        public string? Notes { get; set; }                       // ملاحظات
    }

    public class ProductionOrderResultDto
    {
        public int Id { get; set; }
        public ProductType ProductType { get; set; }
        public string ProductTypeName { get; set; } = string.Empty;
        public int TargetQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public int Difference { get; set; }
        public decimal AchievementPercentage { get; set; }
    }

    public class TreasurySummaryDto
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal LaborExpenses { get; set; }
        public decimal RawMaterialExpenses { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal TotalPaidAmounts { get; set; }
        public decimal TotalRemainingAmountsPayable { get; set; }
        public decimal CashBalance { get; set; }
        public decimal BankTransferBalance { get; set; }
        public decimal InventoryValue { get; set; }
        public decimal ActualProductionValue { get; set; }
        public decimal NetProfit => TotalIncome - TotalExpenses;
    }

    public class DashboardSummaryDto
    {
        public DateTime SelectedDate { get; set; }
        public decimal TodayFlourSacks { get; set; }
        public int TodayTargetProduction { get; set; }
        public int TodayActualProduction { get; set; }
        public decimal TodayExpectedBaskets { get; set; }
        public decimal TodayActualBaskets { get; set; }
        public decimal InventoryTotalValue { get; set; }
        public decimal TodayIncome { get; set; }
        public decimal TodayExpenses { get; set; }
        public decimal TodayNetProfit { get; set; }
        public decimal RemainingAmountsPayable { get; set; }
        public List<LowStockMaterialDto> LowStockMaterials { get; set; } = new();
    }

    public class LowStockMaterialDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public string UnitName { get; set; } = string.Empty;
    }



    public class EmployeeListItemDto 
    { 
    
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public decimal MonthlySalary { get; set; }
        public string WeeklyDayOff { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public int AbsentDaysSinceLastSalary { get; set; }
        public int WorkedDays { get; set; }
        public decimal PendingAdvanceAmount { get; set; }

        public DateTime StartedDate { get; set; }
    }

    public class EmployeeDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal MonthlySalary { get; set; }
        public string WeeklyDayOff { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }

        public decimal PendingAdvanceAmount { get; set; }
        public int AbsentDaysCount { get; set; }
        
        public DateTime StartedDate { get; set; }
        public List<AttendanceItemDto> Attendances { get; set; } = new();
        public List<AdvanceItemDto> Advances { get; set; } = new();
        public List<SalaryExpenseItemDto> SalaryExpenses { get; set; } = new();
    }

    public class AttendanceItemDto
    {
        public DateTime Date { get; set; }
        public bool IsPresent { get; set; }
        public string? Notes { get; set; }
    }

    public class AdvanceItemDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? Notes { get; set; }
    }

    public class SalaryExpenseItemDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
    }

    public class SupplierSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> SuppliedMaterialNames { get; set; } = new();
        public List<int> SuppliedMaterialIds { get; set; } = new();
        public decimal TotalInvoicesValue { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalRemainingAmount { get; set; }
        public int InvoicesCount { get; set; }
        public DateTime? LastTransactionDate { get; set; }
    }

    public class SupplierFinancialSummaryDto
    {
        public decimal TotalInventoryValue { get; set; }
        public decimal TotalIndebtedness { get; set; }
        public decimal TotalPaidToSuppliers { get; set; }
    }

    public class CreateSupplierDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Notes { get; set; }
        public List<int> SelectedRawMaterialIds { get; set; } = new();
    }

    public class CreateSupplierInvoiceDto
    {
        public int SupplierId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public decimal PaidAmount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public string? Notes { get; set; }
        public List<SupplierInvoiceItemInputDto> Items { get; set; } = new();
    }

    public class SupplierInvoiceItemInputDto
    {
        public int RawMaterialId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

}

