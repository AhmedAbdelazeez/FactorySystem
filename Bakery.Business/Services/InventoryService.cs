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
    public interface IInventoryService
    {
        Task<IEnumerable<RawMaterial>> GetAllRawMaterialsAsync();
        Task<RawMaterial?> GetRawMaterialByIdAsync(int id);
        Task AddRawMaterialAsync(RawMaterial material, PaymentMethod paymentMethod, decimal paidAmount = 0, string? notes = null);
        Task DeleteRawMaterialAsync(int id);
        Task<decimal> GetTotalInventoryValueAsync();
        Task AddStockAsync(int rawMaterialId, decimal quantity, decimal unitPrice, PaymentMethod paymentMethod, decimal paidAmount = 0, string? notes = null, int? expenseId = null, int? supplierInvoiceId = null);
        Task DeductStockAsync(int rawMaterialId, decimal quantity, int productionOrderId, string? notes = null);
        Task<IEnumerable<InventoryTransaction>> GetTransactionsAsync(int? rawMaterialId = null);
        Task<InventoryTransaction?> GetTransactionByIdAsync(int id);
        Task UpdateTransactionAsync(int transactionId, decimal newQuantity, decimal newUnitPrice, PaymentMethod paymentMethod, decimal paidAmount, string? notes = null);
        Task DeleteTransactionAsync(int transactionId);
        Task<bool> MaterialTypeExistsAsync(int materialTypeId, int? excludeId = null);
    }

    public class InventoryService : IInventoryService
    {
        private readonly BakeryDbContext _context;
        private readonly IRepository<RawMaterial> _materialRepo;
        private readonly IRepository<InventoryTransaction> _transactionRepo;
        private readonly IRepository<Expense> _expenseRepo;
        private readonly IRepository<TreasuryTransaction> _treasuryRepo;

        public InventoryService(
            BakeryDbContext context,
            IRepository<RawMaterial> materialRepo,
            IRepository<InventoryTransaction> transactionRepo,
            IRepository<Expense> expenseRepo,
            IRepository<TreasuryTransaction> treasuryRepo)
        {
            _context = context;
            _materialRepo = materialRepo;
            _transactionRepo = transactionRepo;
            _expenseRepo = expenseRepo;
            _treasuryRepo = treasuryRepo;
        }

        public async Task<IEnumerable<RawMaterial>> GetAllRawMaterialsAsync()
        {
            return await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .Include(r => r.MaterialType)
                .OrderBy(r => r.MaterialType!.Name)
                .ThenBy(r => r.MeasurementUnit!.Name)
                .ToListAsync();
        }

        public async Task<RawMaterial?> GetRawMaterialByIdAsync(int id)
        {
            return await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .Include(r => r.MaterialType)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task AddRawMaterialAsync(RawMaterial material, PaymentMethod paymentMethod, decimal paidAmount = 0, string? notes = null)
        {
            if (await MaterialTypeExistsAsync(material.MaterialTypeId))
            {
                var type = await _context.MaterialTypes.FindAsync(material.MaterialTypeId);
                throw new InvalidOperationException($"المادة الخام '{type?.Name}' موجودة بالفعل في المخزن، يمكنك توريد كمية جديدة لها بدلاً من الإضافة.");
            }

            if (material.CurrentQuantity < 0)
                throw new InvalidOperationException("لا يمكن أن تكون كمية المخزون بالسالب.");

            if (material.UnitPrice < 0)
                throw new InvalidOperationException("لا يمكن أن يكون السعر بالسالب.");

            bool hasInitialStock = material.CurrentQuantity > 0 && material.UnitPrice > 0;

            if (hasInitialStock)
            {
                decimal initialQty = material.CurrentQuantity;
                decimal initialPrice = material.UnitPrice;

                material.CurrentQuantity = 0;
                material.UnitPrice = 0;
                material.TotalValue = 0;
                material.LastUpdatedDate = DateTime.Now;

                await _materialRepo.AddAsync(material);
                await _materialRepo.SaveChangesAsync();

                await AddStockAsync(material.Id, initialQty, initialPrice, paymentMethod, paidAmount, notes ?? "رصيد افتتاحي عند إضافة المادة");
            }
            else
            {
                material.TotalValue = 0;
                material.LastUpdatedDate = DateTime.Now;
                await _materialRepo.AddAsync(material);
                await _materialRepo.SaveChangesAsync();
            }
        }

        public async Task DeleteRawMaterialAsync(int id)
        {
            var existing = await _materialRepo.GetByIdAsync(id);
            if (existing == null)
                return;

            bool hasTransactions = await _context.InventoryTransactions
                .AnyAsync(t => t.RawMaterialId == id);

            if (hasTransactions)
                throw new InvalidOperationException("لا يمكن حذف هذه المادة لوجود حركات مسجلة عليها في سجل المخزن. يجب حذفها من سجل حركة المخزن أولاً.");

            bool usedInRecipes = await _context.ProductionRecipeItems
                .AnyAsync(r => r.RawMaterialId == id);

            if (usedInRecipes)
                throw new InvalidOperationException("لا يمكن حذف هذه المادة لأنها مستخدمة في وصفة إنتاج. قم بإزالتها من الوصفة أولاً.");

            _materialRepo.Remove(existing);
            await _materialRepo.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalInventoryValueAsync()
        {
            return await _context.RawMaterials.SumAsync(r => r.CurrentQuantity * r.UnitPrice);
        }

        public async Task AddStockAsync(int rawMaterialId, decimal quantity, decimal unitPrice, PaymentMethod paymentMethod, decimal paidAmount = 0, string? notes = null, int? expenseId = null, int? supplierInvoiceId = null)
        {
            if (quantity <= 0) throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

            var material = await _materialRepo.GetByIdAsync(rawMaterialId);
            if (material == null) throw new KeyNotFoundException("المادة الخام غير موجودة.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal newBatchValue = quantity * unitPrice;
                decimal newTotalValue = material.TotalValue + newBatchValue;
                decimal newTotalQuantity = material.CurrentQuantity + quantity;

                material.UnitPrice = newTotalQuantity > 0
                    ? Math.Round(newTotalValue / newTotalQuantity, 4)
                    : unitPrice;

                material.CurrentQuantity = newTotalQuantity;
                material.TotalValue = Math.Round(material.CurrentQuantity * material.UnitPrice, 2);
                material.LastUpdatedDate = DateTime.Now;
                _materialRepo.Update(material);
                await _context.SaveChangesAsync();

                int? linkedExpenseId = expenseId;
                if (linkedExpenseId == null)
                {
                    var materialType = await _context.MaterialTypes.FindAsync(material.MaterialTypeId);
                    var rawMatCategory = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == "مواد خام");
                    int categoryId = rawMatCategory?.Id ?? 1;

                    decimal finalPaidAmount;
                    decimal finalRemainingAmount;

                    if (paymentMethod == PaymentMethod.Cash || paymentMethod == PaymentMethod.BankTransfer)
                    {
                        finalPaidAmount = newBatchValue;
                        finalRemainingAmount = 0;
                    }
                    else if (paymentMethod == PaymentMethod.Unpaid)
                    {
                        finalPaidAmount = 0;
                        finalRemainingAmount = newBatchValue;
                    }
                    else // PartiallyPaid
                    {
                        finalPaidAmount = Math.Min(paidAmount, newBatchValue);
                        finalRemainingAmount = newBatchValue - finalPaidAmount;
                    }

                    var purchaseExpense = new Expense
                    {
                        Name = $"توريد مادة خام: {materialType?.Name}",
                        ExpenseCategoryId = categoryId,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalAmount = newBatchValue,
                        Date = DateTime.Now,
                        PaymentMethod = paymentMethod,
                        PaidAmount = finalPaidAmount,
                        RemainingAmount = finalRemainingAmount,
                        Notes = notes ?? "توريد / إضافة مخزون"
                    };
                    await _expenseRepo.AddAsync(purchaseExpense);
                    await _expenseRepo.SaveChangesAsync();
                    linkedExpenseId = purchaseExpense.Id;

                    if (supplierInvoiceId == null)
                    {
                        var treasuryTx = new TreasuryTransaction
                        {
                            Date = purchaseExpense.Date,
                            TransactionName = purchaseExpense.Name,
                            TransactionType = TreasuryTransactionType.Expense,
                            Category = "مواد خام",
                            Amount = purchaseExpense.TotalAmount,
                            PaymentMethod = purchaseExpense.PaymentMethod,
                            PaidAmount = purchaseExpense.PaidAmount,
                            RemainingAmount = purchaseExpense.RemainingAmount,
                            Notes = purchaseExpense.Notes,
                            ExpenseId = purchaseExpense.Id
                        };
                        await _treasuryRepo.AddAsync(treasuryTx);
                        await _treasuryRepo.SaveChangesAsync();
                    }
                }

                var invTransaction = new InventoryTransaction
                {
                    RawMaterialId = rawMaterialId,
                    TransactionType = TransactionType.Purchase,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalAmount = newBatchValue,
                    TransactionDate = DateTime.Now,
                    ExpenseId = linkedExpenseId,
                    SupplierInvoiceId = supplierInvoiceId,
                    Notes = notes ?? "إضافة / توريد مخزون"
                };

                await _transactionRepo.AddAsync(invTransaction);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeductStockAsync(int rawMaterialId, decimal quantity, int productionOrderId, string? notes = null)
        {
            if (quantity <= 0) throw new InvalidOperationException("كمية الخصم يجب أن تكون أكبر من الصفر.");

            var material = await _context.RawMaterials
                .Include(r => r.MaterialType)
                .FirstOrDefaultAsync(r => r.Id == rawMaterialId);

            if (material == null) throw new KeyNotFoundException("المادة الخام غير موجودة.");

            if (material.CurrentQuantity < quantity)
            {
                throw new InvalidOperationException($"الكمية المتاحة من المادة الخام ({material.MaterialType?.Name}) غير كافية. المتاح: {material.CurrentQuantity} ، المطلوب: {quantity}");
            }

            material.CurrentQuantity -= quantity;
            material.TotalValue = material.CurrentQuantity * material.UnitPrice;
            material.LastUpdatedDate = DateTime.Now;

            _materialRepo.Update(material);

            var transaction = new InventoryTransaction
            {
                RawMaterialId = rawMaterialId,
                TransactionType = TransactionType.ProductionDeduction,
                Quantity = -quantity,
                UnitPrice = material.UnitPrice,
                TotalAmount = -(quantity * material.UnitPrice),
                TransactionDate = DateTime.Now,
                ProductionOrderId = productionOrderId,
                Notes = notes ?? "خصم استهلاك أمر إنتاج"
            };

            await _transactionRepo.AddAsync(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<InventoryTransaction>> GetTransactionsAsync(int? rawMaterialId = null)
        {
            var query = _context.InventoryTransactions
                .Include(t => t.RawMaterial)
                    .ThenInclude(r => r!.MeasurementUnit)
                .Include(t => t.RawMaterial)
                    .ThenInclude(r => r!.MaterialType)
                .Include(t => t.Expense)
                .AsQueryable();

            if (rawMaterialId.HasValue)
            {
                query = query.Where(t => t.RawMaterialId == rawMaterialId.Value);
            }

            return await query.OrderByDescending(t => t.TransactionDate).ToListAsync();
        }

        public async Task<InventoryTransaction?> GetTransactionByIdAsync(int id)
        {
            return await _context.InventoryTransactions
                .Include(t => t.RawMaterial)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task UpdateTransactionAsync(int transactionId, decimal newQuantity, decimal newUnitPrice, PaymentMethod paymentMethod, decimal paidAmount, string? notes = null)
        {
            if (newQuantity <= 0) throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");
            if (newUnitPrice <= 0) throw new InvalidOperationException("يجب أن يكون السعر أكبر من الصفر.");

            var tx = await _context.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transactionId);
            if (tx == null) throw new KeyNotFoundException("الحركة غير موجودة.");

            if (tx.SupplierInvoiceId.HasValue || (tx.Notes != null && tx.Notes.Contains("فاتورة مورد")))
                throw new InvalidOperationException("لا يمكن تعديل هذه الحركة من المخزن لأنها مرتبطة بفاتورة مورد. يرجى تعديل الفاتورة من صفحة الموردين.");

            if (tx.TransactionType != TransactionType.Purchase)
                throw new InvalidOperationException("لا يمكن تعديل حركات خصم الاستهلاك الناتجة عن أوامر الإنتاج.");

            var material = await _materialRepo.GetByIdAsync(tx.RawMaterialId);
            if (material == null) throw new KeyNotFoundException("المادة الخام غير موجودة.");

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal newBatchValue = newQuantity * newUnitPrice;
                decimal quantityDifference = newQuantity - tx.Quantity;
                decimal projectedQuantity = material.CurrentQuantity + quantityDifference;

                if (projectedQuantity < 0)
                    throw new InvalidOperationException($"لا يمكن تعديل هذه الحركة لأن الكمية الناتجة ستكون سالبة (الكمية المتاحة حاليًا: {material.CurrentQuantity}، والفرق المطلوب: {quantityDifference}). قد يكون تم استهلاك جزء من هذه الكمية في أوامر إنتاج لاحقة.");

                decimal materialValueWithoutOldBatch = material.TotalValue - (tx.Quantity * tx.UnitPrice);
                decimal newMaterialTotalValue = materialValueWithoutOldBatch + newBatchValue;

                material.CurrentQuantity = projectedQuantity;
                material.UnitPrice = projectedQuantity > 0 ? Math.Round(newMaterialTotalValue / projectedQuantity, 4) : newUnitPrice;
                material.TotalValue = Math.Round(material.CurrentQuantity * material.UnitPrice, 2);
                material.LastUpdatedDate = DateTime.Now;
                _materialRepo.Update(material);

                tx.Quantity = newQuantity;
                tx.UnitPrice = newUnitPrice;
                tx.TotalAmount = newBatchValue;
                if (!string.IsNullOrWhiteSpace(notes)) tx.Notes = notes;
                _context.InventoryTransactions.Update(tx);

                if (tx.ExpenseId.HasValue)
                {
                    var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == tx.ExpenseId.Value);
                    if (expense != null)
                    {
                        expense.Quantity = newQuantity;
                        expense.UnitPrice = newUnitPrice;
                        expense.TotalAmount = newBatchValue;
                        expense.PaymentMethod = paymentMethod;

                        if (paymentMethod == PaymentMethod.Cash || paymentMethod == PaymentMethod.BankTransfer)
                        {
                            expense.PaidAmount = newBatchValue;
                            expense.RemainingAmount = 0;
                        }
                        else if (paymentMethod == PaymentMethod.Unpaid)
                        {
                            expense.PaidAmount = 0;
                            expense.RemainingAmount = newBatchValue;
                        }
                        else // PartiallyPaid
                        {
                            expense.PaidAmount = paidAmount > newBatchValue ? newBatchValue : paidAmount;
                            expense.RemainingAmount = newBatchValue - expense.PaidAmount;
                        }

                        _context.Expenses.Update(expense);

                        var mainTreasuryTx = await _context.TreasuryTransactions
                            .FirstOrDefaultAsync(t => t.ExpenseId == expense.Id && t.Category != "سداد متبقيات");

                        if (mainTreasuryTx != null)
                        {
                            mainTreasuryTx.Amount = expense.TotalAmount;
                            mainTreasuryTx.PaymentMethod = expense.PaymentMethod;
                            mainTreasuryTx.PaidAmount = expense.PaidAmount;
                            mainTreasuryTx.RemainingAmount = expense.RemainingAmount;
                            _context.TreasuryTransactions.Update(mainTreasuryTx);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteTransactionAsync(int transactionId)
        {
            var tx = await _context.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transactionId);
            if (tx == null) return;

            if (tx.SupplierInvoiceId.HasValue || (tx.Notes != null && tx.Notes.Contains("فاتورة مورد")))
                throw new InvalidOperationException("لا يمكن حذف هذه الحركة من المخزن لأنها مرتبطة بفاتورة مورد. يرجى حذف الفاتورة من صفحة الموردين.");

            if (tx.TransactionType != TransactionType.Purchase)
                throw new InvalidOperationException("لا يمكن حذف حركات خصم الاستهلاك الناتجة عن أوامر الإنتاج.");

            var material = await _materialRepo.GetByIdAsync(tx.RawMaterialId);
            if (material == null) throw new KeyNotFoundException("المادة الخام غير موجودة.");

            decimal projectedQuantity = material.CurrentQuantity - tx.Quantity;

            if (projectedQuantity < 0)
                throw new InvalidOperationException($"لا يمكن حذف هذه الحركة لأن جزءًا من هذه الكمية ({tx.Quantity}) تم استخدامه بالفعل في أوامر إنتاج لاحقة. الكمية المتاحة حاليًا فقط: {material.CurrentQuantity}.");

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (projectedQuantity == 0)
                {
                    material.CurrentQuantity = 0;
                    material.UnitPrice = 0;
                    material.TotalValue = 0;
                }
                else
                {
                    decimal materialValueWithoutBatch = material.TotalValue - (tx.Quantity * tx.UnitPrice);
                    material.CurrentQuantity = projectedQuantity;
                    material.TotalValue = materialValueWithoutBatch < 0 ? 0 : Math.Round(materialValueWithoutBatch, 2);
                    material.UnitPrice = Math.Round(material.TotalValue / projectedQuantity, 4);
                }
                material.LastUpdatedDate = DateTime.Now;
                _materialRepo.Update(material);

                if (tx.ExpenseId.HasValue)
                {
                    var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == tx.ExpenseId.Value);
                    if (expense != null)
                    {
                        var treasuryTxs = await _context.TreasuryTransactions
                            .Where(t => t.ExpenseId == expense.Id)
                            .ToListAsync();

                        if (treasuryTxs.Any())
                            _context.TreasuryTransactions.RemoveRange(treasuryTxs);

                        _context.Expenses.Remove(expense);
                    }
                }

                _context.InventoryTransactions.Remove(tx);

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> MaterialTypeExistsAsync(int materialTypeId, int? excludeId = null)
        {
            var query = _context.RawMaterials.Where(m => m.MaterialTypeId == materialTypeId);

            if (excludeId.HasValue)
                query = query.Where(m => m.Id != excludeId.Value);

            return await query.AnyAsync();
        }
    }
}