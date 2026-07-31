using System;
using System.Collections.Generic;
using Bakery.Domain.Enums;

namespace Bakery.Business.DTOs
{
    public class ProductionCalculationResultDto
    {
        public decimal FlourSackCount { get; set; }
        public int TargetMabroum { get; set; }
        public int TargetPane { get; set; }
        public int TargetSandwich { get; set; }
        public int TotalTargetPieces { get; set; }
        public int ExpectedBaskets { get; set; }
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
        public decimal BasketSellingPrice { get; set; }
        public ProductionStatus Status { get; set; }
        public string StatusName => Status == ProductionStatus.Confirmed ? "مؤكد" : "مسودة";
        public int TotalTargetPieces { get; set; }
        public int TotalActualPieces { get; set; }
        public int ExpectedBaskets { get; set; }
        public int ActualBaskets { get; set; }
        public int RemainingPieces { get; set; }
        public decimal TotalExpectedSalesValue { get; set; }
        public decimal TotalActualSalesValue { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public List<ProductionOrderResultDto> OrderResults { get; set; } = new();
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
        public decimal TodayFlourSacks { get; set; }
        public int TodayTargetProduction { get; set; }
        public int TodayActualProduction { get; set; }
        public int TodayExpectedBaskets { get; set; }
        public int TodayActualBaskets { get; set; }
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
        public decimal PendingAdvanceAmount { get; set; }


    }

}
