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
        Task AddExpenseAsync(Expense expense);//, int? linkedRawMaterialId = null);
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

        public async Task AddExpenseAsync(Expense expense)//, int? linkedRawMaterialId = null)
         {
            //expense.TotalAmount = expense.Quantity * expense.UnitPrice;
            expense.TotalAmount = expense.Quantity * expense.UnitPrice;


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

            // If raw material linked, update stock
            //if (linkedRawMaterialId.HasValue && linkedRawMaterialId.Value > 0)
            //{
            //    await _inventoryService.AddStockAsync(
            //        linkedRawMaterialId.Value,
            //        expense.Quantity,
            //        expense.UnitPrice,
            //         expense.PaymentMethod,   // ← ضفنا ده
            //         expense.PaidAmount,
            //        $"توريد بموجب مصروف: {expense.Name}",
                    
            //        expense.Id
            //    );
            //}
        }

        public async Task UpdateExpenseAsync(Expense expense)
        {
            var existing = await _expenseRepo.GetByIdAsync(expense.Id);
            if (existing == null) throw new KeyNotFoundException("المصروف غير موجود.");

            existing.Name = expense.Name;
            existing.ExpenseCategoryId = expense.ExpenseCategoryId;
            existing.Quantity = expense.Quantity;
            existing.UnitPrice = expense.UnitPrice;
            existing.TotalAmount = expense.Quantity * expense.UnitPrice;
            existing.Date = expense.Date;
            existing.PaymentMethod = expense.PaymentMethod;
            existing.PaidAmount = expense.PaidAmount;
            existing.EmployeeId = expense.EmployeeId;
            existing.Notes = expense.Notes;

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
            else
            {
                if (existing.PaidAmount > existing.TotalAmount)
                    existing.PaidAmount = existing.TotalAmount;
                existing.RemainingAmount = existing.TotalAmount - existing.PaidAmount;
            }

            _expenseRepo.Update(existing);
            await _expenseRepo.SaveChangesAsync();

            // Update associated Treasury Transaction
            var treasuryTx = await _context.TreasuryTransactions.FirstOrDefaultAsync(t => t.ExpenseId == expense.Id);
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
            // التأكد من وجود Transaction مفتوح حالياً أو إنشاء جديد
            var existingTransaction = _context.Database.CurrentTransaction;
            using var transaction = existingTransaction == null
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var expense = await _context.Expenses.FindAsync(expenseId);
                if (expense == null) throw new KeyNotFoundException("المصروف غير موجود.");

                //منع حذف مصروفات التوريد من هنا خالص
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
                    //  إذا كان المصروف عبارة عن "سلفة" تم إضافتها سابقاً
                    if (expense.Name.StartsWith("سلفة للعامل"))
                    {
                        // نجد السلفة المعلقة أو غير المعلقة المرتبطة بهذا العامل ونفس المبلغ/التاريخ
                        var matchingAdvance = await _context.EmployeeAdvances
                            .FirstOrDefaultAsync(a => a.EmployeeId == expense.EmployeeId.Value
                                                   && a.Amount == expense.TotalAmount
                                                   && a.Date.Date == expense.Date.Date);

                        if (matchingAdvance != null)
                        {
                            // نحذف السلفة من الجدول تماماً فتختفي تلقائياً من "السلف المعلقة"
                            _context.EmployeeAdvances.Remove(matchingAdvance);
                        }
                    }
                    // حالة ب: إذا كان المصروف عبارة عن "راتب شهر" تم صرفه
                    else if (expense.Name.StartsWith("راتب العامل"))
                    {
                        // نعيد السلف التي تم تسويتها مع هذا الراتب لتصبح "معلقة" مرة أخرى
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

                //var stockTx = await _context.InventoryTransactions
                //         .FirstOrDefaultAsync(t => t.ExpenseId == expenseId);

                //if (stockTx != null)
                //{
                //    var rawMaterial = await _context.RawMaterials.FindAsync(stockTx.RawMaterialId);
                //    if (rawMaterial != null)
                //    {
                //        // خصم الكمية التي تم توريدها من المخزن
                //        rawMaterial.CurrentQuantity -= stockTx.Quantity;

                //        if (rawMaterial.CurrentQuantity < 0)
                //        {
                //            rawMaterial.CurrentQuantity = 0;
                //        }
                //    }

                //    // حذف سجل حركة المخزن نفسه
                //    _context.InventoryTransactions.Remove(stockTx);
                //}

                // 2. حذف حركة الخزينة المرتبطة بالمصروف (سواء كانت سداد أصلي أو سداد متبقيات
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

                expense.PaidAmount += amountPaidNow;
                expense.RemainingAmount = expense.TotalAmount - expense.PaidAmount;

                if (expense.RemainingAmount == 0)
                {
                    expense.PaymentMethod = paymentMethod;
                }
                else
                {
                    expense.PaymentMethod = PaymentMethod.PartiallyPaid;
                }

                _expenseRepo.Update(expense);

                // تسجيل حركة السداد في الخزينة
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

