using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.DataAccess.Repositories;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;

namespace Bakery.Business.Services
{
    public interface IExpenseService
    {
        Task<IEnumerable<Expense>> GetAllExpensesAsync(DateTime? startDate = null, DateTime? endDate = null, int? categoryId = null);
        Task<Expense?> GetExpenseByIdAsync(int id);
        Task AddExpenseAsync(Expense expense);
        Task UpdateExpenseAsync(Expense expense);
        Task DeleteExpenseAsync(int id);
        Task PayRemainingAsync(int expenseId, decimal amountPaidNow, PaymentMethod paymentMethod);
    }

    public class ExpenseService : IExpenseService
    {
        private readonly BakeryDbContext _context;
        private readonly IRepository<Expense> _expenseRepo;
        private readonly IRepository<TreasuryTransaction> _treasuryRepo;
        private readonly IInventoryService _inventoryService;

        public ExpenseService(
            BakeryDbContext context,
            IRepository<Expense> expenseRepo,
            IRepository<TreasuryTransaction> treasuryRepo,
            IInventoryService inventoryService)
        {
            _context = context;
            _expenseRepo = expenseRepo;
            _treasuryRepo = treasuryRepo;
            _inventoryService = inventoryService;
        }

        public async Task<IEnumerable<Expense>> GetAllExpensesAsync(DateTime? startDate = null, DateTime? endDate = null, int? categoryId = null)
        {
            var query = _context.Expenses
                .Include(e => e.ExpenseCategory)
                .Include(e => e.Employee)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(e => e.Date <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            if (categoryId.HasValue)
                query = query.Where(e => e.ExpenseCategoryId == categoryId.Value);

            return await query.OrderByDescending(e => e.Date).ToListAsync();
        }

        public async Task<Expense?> GetExpenseByIdAsync(int id)
        {
            return await _context.Expenses
                .Include(e => e.ExpenseCategory)
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task AddExpenseAsync(Expense expense)
        {
            if (expense.Quantity <= 0)
            {
                expense.Quantity = 1;
            }

            if (expense.UnitPrice <= 0 && expense.TotalAmount > 0)
            {
                expense.UnitPrice = expense.TotalAmount;
            }
            else
            {
                expense.TotalAmount = expense.Quantity * expense.UnitPrice;
            }

            if (expense.ExpenseCategoryId <= 0)
            {
                var defaultCategory = await _context.ExpenseCategories.FirstOrDefaultAsync();
                if (defaultCategory == null)
                {
                    throw new InvalidOperationException("لا يوجد تصنيفات مصاريف مسجلة بالنظام. يرجى إضافة تصنيف أولاً.");
                }
                expense.ExpenseCategoryId = defaultCategory.Id;
            }

            if (expense.PaymentMethod == PaymentMethod.Cash || expense.PaymentMethod == PaymentMethod.BankTransfer)
            {
                expense.PaidAmount = expense.TotalAmount;
                expense.RemainingAmount = 0;
            }
            else if (expense.PaymentMethod == PaymentMethod.Unpaid)
            {
                expense.PaidAmount = 0;
                expense.RemainingAmount = expense.TotalAmount;
            }
            else // PartiallyPaid
            {
                if (expense.PaidAmount > expense.TotalAmount)
                    expense.PaidAmount = expense.TotalAmount;
                expense.RemainingAmount = expense.TotalAmount - expense.PaidAmount;
            }

            await _expenseRepo.AddAsync(expense);
            await _expenseRepo.SaveChangesAsync();

            // Create Treasury Transaction
            var category = await _context.ExpenseCategories.FindAsync(expense.ExpenseCategoryId);
            var categoryName = category?.Name ?? "مصروفات عامة";

            var treasuryTx = new TreasuryTransaction
            {
                Date = expense.Date,
                TransactionName = expense.Name,
                TransactionType = TreasuryTransactionType.Expense,
                Category = categoryName,
                Amount = expense.TotalAmount,
                PaymentMethod = expense.PaymentMethod,
                PaidAmount = expense.PaidAmount,
                RemainingAmount = expense.RemainingAmount,
                Notes = expense.Notes,
                ExpenseId = expense.Id
            };
            await _treasuryRepo.AddAsync(treasuryTx);
            await _treasuryRepo.SaveChangesAsync();
        }

        public async Task UpdateExpenseAsync(Expense expense)
        {
            var existing = await _expenseRepo.GetByIdAsync(expense.Id);
            if (existing == null) throw new KeyNotFoundException("المصروف غير موجود.");

            // 1. حماية القيود الآلية (رواتب وتوريدات)
            if (existing.Name.StartsWith("راتب العامل") || existing.Name.StartsWith("توريد خامات"))
            {
                throw new InvalidOperationException("لا يمكن تعديل المصروفات الناتجة عن عمليات التوريد أو رواتب العمالة مباشرةً.");
            }

            // 2. تحديث الحقول الأساسية
            existing.Name = expense.Name;
            existing.ExpenseCategoryId = expense.ExpenseCategoryId;
            existing.Quantity = expense.Quantity <= 0 ? 1 : expense.Quantity;
            existing.UnitPrice = expense.UnitPrice <= 0 ? expense.TotalAmount : expense.UnitPrice;
            existing.TotalAmount = expense.TotalAmount > 0 ? expense.TotalAmount : (existing.Quantity * existing.UnitPrice);
            existing.Date = expense.Date;
            existing.PaymentMethod = expense.PaymentMethod;
            existing.Notes = expense.Notes;

            // 3. ضبط المبالغ بناءً على طريقة الدفع
            if (existing.PaymentMethod == PaymentMethod.Cash || existing.PaymentMethod == PaymentMethod.BankTransfer)
            {
                existing.PaidAmount = existing.TotalAmount;
                existing.RemainingAmount = 0;
            }
            else if (existing.PaymentMethod == PaymentMethod.Unpaid)
            {
                existing.PaidAmount = 0;
                existing.RemainingAmount = existing.TotalAmount;
            }
            else // PartiallyPaid
            {
                existing.PaidAmount = expense.PaidAmount > existing.TotalAmount ? existing.TotalAmount : expense.PaidAmount;
                existing.RemainingAmount = existing.TotalAmount - existing.PaidAmount;
            }

            _expenseRepo.Update(existing);
            await _expenseRepo.SaveChangesAsync();

            // 4. تحديث حركة الخزينة المرتبطة تلقائياً (استبعاد حركات سداد المتبقيات)
            var treasuryTx = await _context.TreasuryTransactions
                .FirstOrDefaultAsync(t => t.ExpenseId == expense.Id && t.Category != "سداد متبقيات");

            if (treasuryTx != null)
            {
                var category = await _context.ExpenseCategories.FindAsync(existing.ExpenseCategoryId);

                treasuryTx.Date = existing.Date;
                treasuryTx.TransactionName = existing.Name;
                treasuryTx.Category = category?.Name ?? "مصروفات";
                treasuryTx.Amount = existing.TotalAmount;
                treasuryTx.PaymentMethod = existing.PaymentMethod;
                treasuryTx.PaidAmount = existing.PaidAmount;
                treasuryTx.RemainingAmount = existing.RemainingAmount;
                treasuryTx.Notes = existing.Notes;

                _treasuryRepo.Update(treasuryTx);
                await _treasuryRepo.SaveChangesAsync();
            }
        }

        public async Task DeleteExpenseAsync(int expenseId)
        {
            var existingTransaction = _context.Database.CurrentTransaction;
            using var transaction = existingTransaction == null
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var expense = await _context.Expenses.FindAsync(expenseId);
                if (expense == null) throw new KeyNotFoundException("المصروف غير موجود.");

                bool isLinkedToInventory = await _context.InventoryTransactions
                    .AnyAsync(t => t.ExpenseId == expenseId);

                if (isLinkedToInventory)
                {
                    throw new InvalidOperationException(
                        "هذا المصروف ناتج عن عملية توريد مخزون، لا يمكن حذفه من هنا. يرجى حذفه أو تعديله من صفحة \"سجل حركة المخزن\" فقط.");
                }

                // 1. التعامل مع مصروفات العمالة (الرواتب والسُلف)
                if (expense.EmployeeId.HasValue)
                {
                    if (expense.Name.StartsWith("سلفة للعامل"))
                    {
                        var matchingAdvance = await _context.EmployeeAdvances
                            .FirstOrDefaultAsync(a => a.EmployeeId == expense.EmployeeId.Value
                                                   && a.Amount == expense.TotalAmount
                                                   && a.Date.Date == expense.Date.Date);

                        if (matchingAdvance != null)
                        {
                            _context.EmployeeAdvances.Remove(matchingAdvance);
                        }
                    }
                    else if (expense.Name.StartsWith("راتب العامل"))
                    {
                        var paidAdvances = await _context.EmployeeAdvances
                            .Where(a => a.EmployeeId == expense.EmployeeId.Value && a.PaidDate.HasValue && a.PaidDate.Value.Date == expense.Date.Date)
                            .ToListAsync();

                        foreach (var advance in paidAdvances)
                        {
                            advance.IsPaid = false;
                            advance.PaidDate = null;
                        }
                    }
                }

                // 2. حذف جميع حركات الخزينة المرتبطة بالمصروف (السداد الأصلي وسداد المتبقيات)
                var relatedTreasuryTxs = await _context.TreasuryTransactions
                    .Where(t => t.ExpenseId == expenseId)
                    .ToListAsync();

                if (relatedTreasuryTxs.Any())
                {
                    _context.TreasuryTransactions.RemoveRange(relatedTreasuryTxs);
                }

                // 3. حذف المصروف نفسه
                _expenseRepo.Remove(expense);

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
        }

        public async Task PayRemainingAsync(int expenseId, decimal amountPaidNow, PaymentMethod paymentMethod)
        {
            if (amountPaidNow <= 0)
                throw new InvalidOperationException("المبلغ المدفوع يجب أن يكون أكبر من الصفر.");

            var existingTransaction = _context.Database.CurrentTransaction;
            using var transaction = existingTransaction == null
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var expense = await _expenseRepo.GetByIdAsync(expenseId);
                if (expense == null) throw new KeyNotFoundException("المصروف غير موجود.");

                if (amountPaidNow > expense.RemainingAmount)
                    throw new InvalidOperationException($"المبلغ المدفوع ({amountPaidNow:N2}) أكبر من المتبقي ({expense.RemainingAmount:N2}).");

                // 1. تحديث أرقام المصروف الرئيسي
                expense.PaidAmount += amountPaidNow;
                expense.RemainingAmount = expense.TotalAmount - expense.PaidAmount;

                if (expense.RemainingAmount <= 0)
                {
                    expense.RemainingAmount = 0;
                    expense.PaymentMethod = paymentMethod; // تحويله للحالة النهائية (مثل الكاش)
                }
                else
                {
                    expense.PaymentMethod = PaymentMethod.PartiallyPaid;
                }

                _expenseRepo.Update(expense);

                // 2. تحديث حركة الخزينة الأصلية لتنعكس التغيرات بها في الجدول السفلّي
                var mainTreasuryTx = await _context.TreasuryTransactions
                    .FirstOrDefaultAsync(t => t.ExpenseId == expense.Id && t.Category != "سداد متبقيات");

                if (mainTreasuryTx != null)
                {
                    mainTreasuryTx.PaidAmount = expense.PaidAmount;
                    mainTreasuryTx.RemainingAmount = expense.RemainingAmount;
                    mainTreasuryTx.PaymentMethod = expense.PaymentMethod;

                    _treasuryRepo.Update(mainTreasuryTx);
                }

                // 3. تسجيل حركة السداد الجديدة في الخزينة (تسجيل تدفق نقدي بتاريخ اليوم)
                var treasuryTx = new TreasuryTransaction
                {
                    Date = DateTime.Now,
                    TransactionName = $"سداد متبقي مصروف: {expense.Name}",
                    TransactionType = TreasuryTransactionType.Expense,
                    Category = "سداد متبقيات",
                    Amount = amountPaidNow,
                    PaymentMethod = paymentMethod,
                    PaidAmount = amountPaidNow,
                    RemainingAmount = 0,
                    Notes = $"سداد دفعة للمصروف رقم #{expense.Id}",
                    ExpenseId = expense.Id
                };

                await _treasuryRepo.AddAsync(treasuryTx);

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
        }
    }
}