using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;
using Bakery.Business.DTOs;

namespace Bakery.Business.Services
{
    public interface ITreasuryService
    {
        Task<TreasurySummaryDto> GetTreasurySummaryAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<TreasuryTransaction>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, TreasuryTransactionType? type = null);
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(DateTime date);
    }

    public class TreasuryService : ITreasuryService
    {
        private readonly BakeryDbContext _context;

        public TreasuryService(BakeryDbContext context)
        {
            _context = context;
        }

        public async Task<TreasurySummaryDto> GetTreasurySummaryAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var txQuery = _context.TreasuryTransactions.AsQueryable();

            if (startDate.HasValue)
                txQuery = txQuery.Where(t => t.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                txQuery = txQuery.Where(t => t.Date <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            var transactions = await txQuery.ToListAsync();

            decimal totalIncome = transactions
                .Where(t => t.TransactionType == TreasuryTransactionType.Income)
                .Sum(t => t.Amount);

            // ✅ FIX: Exclude "سداد متبقيات" from TotalExpenses to prevent double-counting.
            // When a deferred expense is paid, PayRemainingAsync creates a new treasury record
            // with Category = "سداد متبقيات". The original record already holds the full TotalAmount,
            // so including the payment record would count the same money twice.
            decimal totalExpenses = transactions
                .Where(t => t.TransactionType == TreasuryTransactionType.Expense
                         && t.Category != "سداد متبقيات")
                .Sum(t => t.Amount);

            // Expenses breakdown — sourced from Expenses table for accuracy
            var expenseQuery = _context.Expenses.Include(e => e.ExpenseCategory).AsQueryable();
            if (startDate.HasValue) expenseQuery = expenseQuery.Where(e => e.Date >= startDate.Value.Date);
            if (endDate.HasValue) expenseQuery = expenseQuery.Where(e => e.Date <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            var expensesList = await expenseQuery.ToListAsync();
            decimal labor = expensesList.Where(e => e.ExpenseCategory?.Name == "عمالة").Sum(e => e.TotalAmount);
            decimal rawMaterial = expensesList.Where(e => e.ExpenseCategory?.Name == "مواد خام").Sum(e => e.TotalAmount);
            decimal operating = expensesList.Where(e => e.ExpenseCategory?.Name != "عمالة" && e.ExpenseCategory?.Name != "مواد خام").Sum(e => e.TotalAmount);

            decimal paidAmounts = transactions
                .Where(t => t.Category != "سداد متبقيات") // avoid counting payment twice
                .Sum(t => t.PaidAmount);

            // RemainingAmountsPayable: pulled from Expenses table directly for accuracy
            decimal remainingPayable = expensesList.Sum(e => e.RemainingAmount);

            // Cash and Bank Balances
            decimal cashIncomePaid = transactions
                .Where(t => t.TransactionType == TreasuryTransactionType.Income && t.PaymentMethod == PaymentMethod.Cash)
                .Sum(t => t.PaidAmount);

            decimal cashExpensePaid = transactions
                .Where(t => t.TransactionType == TreasuryTransactionType.Expense && t.PaymentMethod == PaymentMethod.Cash)
                .Sum(t => t.PaidAmount);

            decimal cashBalance = cashIncomePaid - cashExpensePaid;

            decimal bankIncomePaid = transactions
                .Where(t => t.TransactionType == TreasuryTransactionType.Income && t.PaymentMethod == PaymentMethod.BankTransfer)
                .Sum(t => t.PaidAmount);

            decimal bankExpensePaid = transactions
                .Where(t => t.TransactionType == TreasuryTransactionType.Expense && t.PaymentMethod == PaymentMethod.BankTransfer)
                .Sum(t => t.PaidAmount);

            decimal bankBalance = bankIncomePaid - bankExpensePaid;

            // Inventory & Actual Production Values
            decimal inventoryVal = await _context.RawMaterials.SumAsync(r => r.CurrentQuantity * r.UnitPrice);

            decimal actualProdVal = await _context.ProductionOrders
                .Where(o => o.Status == ProductionStatus.Confirmed && o.ActualBaskets > 0)
                .SumAsync(o => o.ActualBaskets * o.BasketSellingPrice);

            return new TreasurySummaryDto
            {
                TotalIncome = totalIncome,
                TotalExpenses = totalExpenses,
                LaborExpenses = labor,
                RawMaterialExpenses = rawMaterial,
                OperatingExpenses = operating,
                TotalPaidAmounts = paidAmounts,
                TotalRemainingAmountsPayable = remainingPayable,
                CashBalance = cashBalance,
                BankTransferBalance = bankBalance,
                InventoryValue = inventoryVal,
                ActualProductionValue = actualProdVal
            };
        }

        public async Task<IEnumerable<TreasuryTransaction>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, TreasuryTransactionType? type = null)
        {
            var query = _context.TreasuryTransactions.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            if (type.HasValue)
                query = query.Where(t => t.TransactionType == type.Value);

            return await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync();
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(DateTime date)
        {
            var today = date.Date;

            var todayOrders = await _context.ProductionOrders
                .Where(o =>
                    o.ProductionDate >= today &&
                    o.ProductionDate < today.AddDays(1))
                .ToListAsync();

            decimal todaySacks = todayOrders.Sum(o => o.FlourSackCount);
            int todayTarget = todayOrders.Sum(o => o.TotalTargetPieces);
            int todayActual = todayOrders.Sum(o => o.TotalActualPieces);
            decimal todayExpBaskets = todayOrders.Sum(o => o.ExpectedBaskets);
            decimal todayActBaskets = todayOrders.Sum(o => o.ActualBaskets);

            var treasurySummary = await GetTreasurySummaryAsync(today, today);
            var overallSummary = await GetTreasurySummaryAsync();

            var lowStock = await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .Include(r => r.MaterialType)
                .Where(r => r.CurrentQuantity <= 10)
                .Select(r => new LowStockMaterialDto
                {
                    Id = r.Id,
                    Name = r.MaterialType != null ? r.MaterialType.Name : "",
                    CurrentQuantity = r.CurrentQuantity,
                    UnitName = r.MeasurementUnit != null ? r.MeasurementUnit.Name : ""
                })
                .ToListAsync();

            return new DashboardSummaryDto
            {
                SelectedDate = today,
                TodayFlourSacks = todaySacks,
                TodayTargetProduction = todayTarget,
                TodayActualProduction = todayActual,
                TodayExpectedBaskets = todayExpBaskets,
                TodayActualBaskets = todayActBaskets,
                InventoryTotalValue = overallSummary.InventoryValue,
                TodayIncome = treasurySummary.TotalIncome,
                TodayExpenses = treasurySummary.TotalExpenses,
                TodayNetProfit = treasurySummary.NetProfit,
                RemainingAmountsPayable = overallSummary.TotalRemainingAmountsPayable,
                LowStockMaterials = lowStock
            };
        }
    }
}