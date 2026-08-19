using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.DataAccess.Repositories;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;
using Bakery.Business.DTOs;

namespace Bakery.Business.Services
{
    public interface ISupplierService
    {
        Task<IEnumerable<SupplierSummaryDto>> GetAllSuppliersSummaryAsync();
        Task<SupplierSummaryDto?> GetSupplierSummaryByIdAsync(int id);
        Task<Supplier?> GetSupplierDetailsByIdAsync(int id);
        Task AddSupplierAsync(CreateSupplierDto dto);
        Task UpdateSupplierAsync(CreateSupplierDto dto);
        Task DeleteSupplierAsync(int id);
        Task<SupplierInvoice> AddInvoiceAsync(CreateSupplierInvoiceDto dto);
        Task UpdateInvoiceAsync(EditSupplierInvoiceDto dto);
        Task DeleteInvoiceAsync(int invoiceId);
        Task PayInvoiceRemainingAsync(int invoiceId, decimal amountPaidNow, PaymentMethod paymentMethod);
        Task<IEnumerable<SupplierInvoice>> GetAllInvoicesAsync();
        Task<SupplierInvoice?> GetInvoiceByIdAsync(int id);
        Task<SupplierFinancialSummaryDto> GetFinancialSummaryAsync();
    }

    public class SupplierService : ISupplierService
    {
        private readonly BakeryDbContext _context;
        private readonly IRepository<Supplier> _supplierRepo;
        private readonly IRepository<SupplierInvoice> _invoiceRepo;
        private readonly IRepository<TreasuryTransaction> _treasuryRepo;
        private readonly IInventoryService _inventoryService;

        public SupplierService(
            BakeryDbContext context,
            IRepository<Supplier> supplierRepo,
            IRepository<SupplierInvoice> invoiceRepo,
            IRepository<TreasuryTransaction> treasuryRepo,
            IInventoryService inventoryService)
        {
            _context = context;
            _supplierRepo = supplierRepo;
            _invoiceRepo = invoiceRepo;
            _treasuryRepo = treasuryRepo;
            _inventoryService = inventoryService;
        }

        public async Task<IEnumerable<SupplierSummaryDto>> GetAllSuppliersSummaryAsync()
        {
            var suppliers = await _context.Suppliers
                .Include(s => s.SuppliedMaterials)
                    .ThenInclude(sm => sm.RawMaterial)
                        .ThenInclude(rm => rm!.MaterialType)
                .Include(s => s.Invoices)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return suppliers.Select(s => new SupplierSummaryDto
            {
                Id = s.Id,
                Name = s.Name,
                Phone = s.Phone,
                Notes = s.Notes,
                CreatedAt = s.CreatedAt,
                SuppliedMaterialNames = s.SuppliedMaterials.Where(sm => sm.RawMaterial != null).Select(sm => sm.RawMaterial!.MaterialName).ToList(),
                SuppliedMaterialIds = s.SuppliedMaterials.Select(sm => sm.RawMaterialId).ToList(),
                TotalInvoicesValue = s.Invoices.Sum(i => i.TotalAmount),
                TotalPaidAmount = s.Invoices.Sum(i => i.PaidAmount),
                TotalRemainingAmount = s.Invoices.Sum(i => i.RemainingAmount),
                InvoicesCount = s.Invoices.Count,
                LastTransactionDate = s.Invoices.Any()
                    ? s.Invoices.Max(i => i.InvoiceDate)
                    : s.CreatedAt
            }).ToList();
        }

        public async Task<SupplierSummaryDto?> GetSupplierSummaryByIdAsync(int id)
        {
            var s = await _context.Suppliers
                .Include(s => s.SuppliedMaterials)
                    .ThenInclude(sm => sm.RawMaterial)
                        .ThenInclude(rm => rm!.MaterialType)
                .Include(s => s.Invoices)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (s == null) return null;

            return new SupplierSummaryDto
            {
                Id = s.Id,
                Name = s.Name,
                Phone = s.Phone,
                Notes = s.Notes,
                CreatedAt = s.CreatedAt,
                SuppliedMaterialNames = s.SuppliedMaterials.Where(sm => sm.RawMaterial != null).Select(sm => sm.RawMaterial!.MaterialName).ToList(),
                SuppliedMaterialIds = s.SuppliedMaterials.Select(sm => sm.RawMaterialId).ToList(),
                TotalInvoicesValue = s.Invoices.Sum(i => i.TotalAmount),
                TotalPaidAmount = s.Invoices.Sum(i => i.PaidAmount),
                TotalRemainingAmount = s.Invoices.Sum(i => i.RemainingAmount),
                InvoicesCount = s.Invoices.Count,
                LastTransactionDate = s.Invoices.Any()
                    ? s.Invoices.Max(i => i.InvoiceDate)
                    : s.CreatedAt
            };
        }


        public async Task<Supplier?> GetSupplierDetailsByIdAsync(int id)
        {
            return await _context.Suppliers
                .Include(s => s.SuppliedMaterials)
                    .ThenInclude(sm => sm.RawMaterial)
                        .ThenInclude(rm => rm!.MeasurementUnit)
                .Include(s => s.Invoices.OrderByDescending(i => i.InvoiceDate))
                    .ThenInclude(i => i.Items)
                        .ThenInclude(item => item.RawMaterial)
                            .ThenInclude(rm => rm!.MeasurementUnit)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task AddSupplierAsync(CreateSupplierDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("اسم المورد مطلوب.");

            var supplier = new Supplier
            {
                Name = dto.Name.Trim(),
                Phone = dto.Phone,
                Notes = dto.Notes,
                CreatedAt = DateTime.Now
            };

            if (dto.SelectedRawMaterialIds != null && dto.SelectedRawMaterialIds.Any())
            {
                foreach (var matId in dto.SelectedRawMaterialIds.Distinct())
                {
                    supplier.SuppliedMaterials.Add(new SupplierRawMaterial
                    {
                        RawMaterialId = matId
                    });
                }
            }

            await _supplierRepo.AddAsync(supplier);
            await _supplierRepo.SaveChangesAsync();
        }

        public async Task UpdateSupplierAsync(CreateSupplierDto dto)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.SuppliedMaterials)
                .FirstOrDefaultAsync(s => s.Id == dto.Id);

            if (supplier == null)
                throw new KeyNotFoundException("المورد غير موجود.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("اسم المورد مطلوب.");

            supplier.Name = dto.Name.Trim();
            supplier.Phone = dto.Phone;
            supplier.Notes = dto.Notes;

            // Update supplied materials checklist
            _context.SupplierRawMaterials.RemoveRange(supplier.SuppliedMaterials);

            if (dto.SelectedRawMaterialIds != null && dto.SelectedRawMaterialIds.Any())
            {
                foreach (var matId in dto.SelectedRawMaterialIds.Distinct())
                {
                    supplier.SuppliedMaterials.Add(new SupplierRawMaterial
                    {
                        SupplierId = supplier.Id,
                        RawMaterialId = matId
                    });
                }
            }

            _supplierRepo.Update(supplier);
            await _supplierRepo.SaveChangesAsync();
        }

        public async Task DeleteSupplierAsync(int id)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);
            if (supplier != null)
            {
                _supplierRepo.Remove(supplier);
                await _supplierRepo.SaveChangesAsync();
            }
        }

        public async Task<SupplierInvoice> AddInvoiceAsync(CreateSupplierInvoiceDto dto)
        {
            var supplier = await _context.Suppliers.FindAsync(dto.SupplierId);
            if (supplier == null)
                throw new KeyNotFoundException("المورد المأخوذ منه الفاتورة غير موجود.");

            var validItems = dto.Items?.Where(i => i.Quantity > 0 && i.UnitPrice >= 0).ToList();
            if (validItems == null || !validItems.Any())
                throw new InvalidOperationException("الفاتورة يجب أن تحتوي على بنود مادية صحيحة (كمية وسعر).");

            decimal totalAmount = validItems.Sum(i => i.Quantity * i.UnitPrice);
            decimal paidAmount = CalculatePaidAmount(dto.PaymentMethod, dto.PaidAmount, totalAmount);
            decimal remainingAmount = totalAmount - paidAmount;

            string invNum = string.IsNullOrWhiteSpace(dto.InvoiceNumber)
                ? $"INV-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}"
                : dto.InvoiceNumber.Trim();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var invoice = new SupplierInvoice
                {
                    SupplierId = dto.SupplierId,
                    InvoiceNumber = invNum,
                    InvoiceDate = dto.InvoiceDate,
                    TotalAmount = totalAmount,
                    PaidAmount = paidAmount,
                    RemainingAmount = remainingAmount,
                    PaymentMethod = dto.PaymentMethod,
                    Notes = dto.Notes,
                    CreatedAt = DateTime.Now
                };

                foreach (var item in validItems)
                {
                    invoice.Items.Add(new SupplierInvoiceItem
                    {
                        RawMaterialId = item.RawMaterialId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalAmount = item.Quantity * item.UnitPrice
                    });
                }

                await _context.SupplierInvoices.AddAsync(invoice);
                await _context.SaveChangesAsync(); // للحصول على invoice.Id

                // ⚡ إضافة المخزون للبنود ⚡
                decimal paidRatio = totalAmount > 0 ? paidAmount / totalAmount : 0;
                foreach (var item in validItems)
                {
                    decimal itemTotal = item.Quantity * item.UnitPrice;
                    decimal itemPaid = Math.Round(itemTotal * paidRatio, 2);

                    await _inventoryService.AddStockAsync(
                        item.RawMaterialId,
                        item.Quantity,
                        item.UnitPrice,
                        invoice.PaymentMethod,
                        itemPaid,
                        $"توريد بموجب فاتورة مورد #{invoice.InvoiceNumber} - {supplier.Name}",
                        null,
                        invoice.Id
                    );
                }

                // ⚡ تسجيل حركة الخزينة ⚡
                if (paidAmount > 0)
                {
                    var treasuryTx = new TreasuryTransaction
                    {
                        Date = invoice.InvoiceDate,
                        TransactionName = $"فاتورة توريد خامات #{invoice.InvoiceNumber} ({supplier.Name})",
                        TransactionType = TreasuryTransactionType.Expense,
                        Category = "مواد خام",
                        Amount = totalAmount,
                        PaymentMethod = invoice.PaymentMethod,
                        PaidAmount = paidAmount,
                        RemainingAmount = remainingAmount,
                        Notes = $"مورد: {supplier.Name} | {dto.Notes}",
                        SupplierInvoiceId = invoice.Id
                    };
                    await _context.TreasuryTransactions.AddAsync(treasuryTx);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return invoice;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateInvoiceAsync(EditSupplierInvoiceDto dto)
        {
            var invoice = await _context.SupplierInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.Id == dto.Id);

            if (invoice == null)
                throw new KeyNotFoundException("الفاتورة غير موجودة.");

            var supplier = await _context.Suppliers.FindAsync(dto.SupplierId);
            if (supplier == null)
                throw new KeyNotFoundException("المورد غير موجود.");

            var validItems = dto.Items?.Where(i => i.Quantity > 0 && i.UnitPrice >= 0).ToList();
            if (validItems == null || !validItems.Any())
                throw new InvalidOperationException("الفاتورة يجب أن تحتوي على بنود مادية صحيحة (كمية وسعر).");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. العودة عن المخزون القديم
                foreach (var oldItem in invoice.Items)
                {
                    var material = await _context.RawMaterials.FindAsync(oldItem.RawMaterialId);
                    if (material != null)
                    {
                        decimal projectedQuantity = material.CurrentQuantity - oldItem.Quantity;
                        if (projectedQuantity < 0)
                        {
                            throw new InvalidOperationException($"لا يمكن تعديل الفاتورة لأن الكمية المتبقية من المادة الخام ({material.MaterialName}) ستصبح سالبة ({projectedQuantity}). قد يكون تم استخدام جزء من الكمية القديمة في أوامر إنتاج لاحقة.");
                        }

                        if (projectedQuantity == 0)
                        {
                            material.CurrentQuantity = 0;
                            material.UnitPrice = 0;
                            material.TotalValue = 0;
                        }
                        else
                        {
                            decimal materialValueWithoutBatch = material.TotalValue - oldItem.TotalAmount;
                            material.CurrentQuantity = projectedQuantity;
                            material.TotalValue = Math.Max(0, Math.Round(materialValueWithoutBatch, 2));
                            material.UnitPrice = Math.Round(material.TotalValue / projectedQuantity, 4);
                        }
                        material.LastUpdatedDate = DateTime.Now;
                    }
                }

                // 2. حذف الحركات القديمة المرتبطة بالفاتورة
                var oldInvTxs = await _context.InventoryTransactions
                    .Where(t => t.SupplierInvoiceId == invoice.Id)
                    .ToListAsync();

                var oldExpenseIds = oldInvTxs.Where(t => t.ExpenseId.HasValue).Select(t => t.ExpenseId.Value).ToList();
                if (oldExpenseIds.Any())
                {
                    var oldExpenses = await _context.Expenses
                        .Where(e => oldExpenseIds.Contains(e.Id))
                        .ToListAsync();
                    _context.Expenses.RemoveRange(oldExpenses);
                }

                _context.InventoryTransactions.RemoveRange(oldInvTxs);

                // 3. مسح بنود الفاتورة القديمة
                _context.SupplierInvoiceItems.RemoveRange(invoice.Items);

                // 4. الحسابات الجديدة
                decimal totalAmount = validItems.Sum(i => i.Quantity * i.UnitPrice);
                decimal paidAmount = CalculatePaidAmount(dto.PaymentMethod, dto.PaidAmount, totalAmount);
                decimal remainingAmount = totalAmount - paidAmount;

                string invNum = string.IsNullOrWhiteSpace(dto.InvoiceNumber)
                    ? $"INV-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}"
                    : dto.InvoiceNumber.Trim();

                // 5. تحديث رأس الفاتورة
                invoice.SupplierId = dto.SupplierId;
                invoice.InvoiceNumber = invNum;
                invoice.InvoiceDate = dto.InvoiceDate;
                invoice.TotalAmount = totalAmount;
                invoice.PaidAmount = paidAmount;
                invoice.RemainingAmount = remainingAmount;
                invoice.PaymentMethod = dto.PaymentMethod;
                invoice.Notes = dto.Notes;

                // 6. إضافة البنود الجديدة
                foreach (var item in validItems)
                {
                    invoice.Items.Add(new SupplierInvoiceItem
                    {
                        RawMaterialId = item.RawMaterialId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalAmount = item.Quantity * item.UnitPrice
                    });
                }

                await _context.SaveChangesAsync(); // تطبيق مسح البنود السابقة وحفظ البنود الجديدة

                // 7. إعادة تطبيق إضافات المخزون
                decimal paidRatio = totalAmount > 0 ? paidAmount / totalAmount : 0;
                foreach (var item in validItems)
                {
                    decimal itemTotal = item.Quantity * item.UnitPrice;
                    decimal itemPaid = Math.Round(itemTotal * paidRatio, 2);

                    await _inventoryService.AddStockAsync(
                        item.RawMaterialId,
                        item.Quantity,
                        item.UnitPrice,
                        invoice.PaymentMethod,
                        itemPaid,
                        $"توريد بموجب فاتورة مورد #{invoice.InvoiceNumber} - {supplier.Name}",
                        null,
                        invoice.Id
                    );
                }

                // 8. تحديث الخزينة
                var treasuryTx = await _context.TreasuryTransactions
                    .FirstOrDefaultAsync(t => t.SupplierInvoiceId == invoice.Id);

                if (paidAmount > 0)
                {
                    if (treasuryTx != null)
                    {
                        treasuryTx.Date = invoice.InvoiceDate;
                        treasuryTx.TransactionName = $"فاتورة توريد خامات #{invoice.InvoiceNumber} ({supplier.Name})";
                        treasuryTx.PaymentMethod = invoice.PaymentMethod;
                        treasuryTx.Amount = totalAmount;
                        treasuryTx.PaidAmount = paidAmount;
                        treasuryTx.RemainingAmount = remainingAmount;
                        treasuryTx.Notes = $"مورد: {supplier.Name} | {dto.Notes}";
                    }
                    else
                    {
                        await _context.TreasuryTransactions.AddAsync(new TreasuryTransaction
                        {
                            Date = invoice.InvoiceDate,
                            TransactionName = $"فاتورة توريد خامات #{invoice.InvoiceNumber} ({supplier.Name})",
                            TransactionType = TreasuryTransactionType.Expense,
                            Category = "مواد خام",
                            Amount = totalAmount,
                            PaymentMethod = invoice.PaymentMethod,
                            PaidAmount = paidAmount,
                            RemainingAmount = remainingAmount,
                            Notes = $"مورد: {supplier.Name} | {dto.Notes}",
                            SupplierInvoiceId = invoice.Id
                        });
                    }
                }
                else if (treasuryTx != null)
                {
                    _context.TreasuryTransactions.Remove(treasuryTx);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // 💡 دالة مساعدة لتجميع منطق حساب المدفوع
        private static decimal CalculatePaidAmount(PaymentMethod method, decimal inputPaid, decimal total)
        {
            if (inputPaid > total)
                throw new InvalidOperationException("المبلغ المدفوع لا يمكن أن يكون أكبر من إجمالي قيمة الفاتورة.");

            return method switch
            {
                PaymentMethod.Cash or PaymentMethod.BankTransfer => total,
                PaymentMethod.Unpaid => 0,
                PaymentMethod.PartiallyPaid => Math.Max(0, inputPaid),
                _ => inputPaid
            };
        }

        public async Task DeleteInvoiceAsync(int invoiceId)
        {
            var invoice = await _context.SupplierInvoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new KeyNotFoundException("الفاتورة غير موجودة.");

            using (var dbTransaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Check and Revert Raw Material stock counts
                    foreach (var oldItem in invoice.Items)
                    {
                        var material = await _context.RawMaterials.FindAsync(oldItem.RawMaterialId);
                        if (material != null)
                        {
                            decimal projectedQuantity = material.CurrentQuantity - oldItem.Quantity;
                            if (projectedQuantity < 0)
                            {
                                throw new InvalidOperationException($"لا يمكن حذف الفاتورة لأن الكمية المتبقية من المادة الخام ({material.MaterialName}) ستصبح سالبة ({projectedQuantity}). قد يكون تم استخدام جزء من الكمية القديمة في أوامر إنتاج لاحقة.");
                            }

                            if (projectedQuantity == 0)
                            {
                                material.CurrentQuantity = 0;
                                material.UnitPrice = 0;
                                material.TotalValue = 0;
                            }
                            else
                            {
                                decimal materialValueWithoutBatch = material.TotalValue - oldItem.TotalAmount;
                                material.CurrentQuantity = projectedQuantity;
                                material.TotalValue = materialValueWithoutBatch < 0 ? 0 : Math.Round(materialValueWithoutBatch, 2);
                                material.UnitPrice = Math.Round(material.TotalValue / projectedQuantity, 4);
                            }
                            material.LastUpdatedDate = DateTime.Now;
                            _context.RawMaterials.Update(material);
                        }
                    }
                    await _context.SaveChangesAsync();

                    // 2. Remove associated Inventory Transactions and Expense records
                    var invTxs = await _context.InventoryTransactions
                        .Where(t => t.SupplierInvoiceId == invoice.Id)
                        .ToListAsync();

                    var expenseIds = invTxs.Where(t => t.ExpenseId.HasValue).Select(t => t.ExpenseId.Value).ToList();
                    if (expenseIds.Any())
                    {
                        var expenses = await _context.Expenses
                            .Where(e => expenseIds.Contains(e.Id))
                            .ToListAsync();
                        _context.Expenses.RemoveRange(expenses);
                    }

                    _context.InventoryTransactions.RemoveRange(invTxs);
                    await _context.SaveChangesAsync();

                    // 3. Remove associated Treasury Transaction
                    var treasuryTx = await _context.TreasuryTransactions
                        .FirstOrDefaultAsync(t => t.SupplierInvoiceId == invoice.Id);
                    if (treasuryTx != null)
                    {
                        _context.TreasuryTransactions.Remove(treasuryTx);
                    }

                    // 4. Remove supplier invoice items
                    _context.SupplierInvoiceItems.RemoveRange(invoice.Items);

                    // 5. Remove the invoice itself
                    _context.SupplierInvoices.Remove(invoice);

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();
                }
                catch (Exception)
                {
                    await dbTransaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task PayInvoiceRemainingAsync(int invoiceId, decimal amountPaidNow, PaymentMethod paymentMethod)
        {
            var invoice = await _context.SupplierInvoices
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new KeyNotFoundException("الفاتورة غير موجودة.");

            if (amountPaidNow <= 0)
                throw new InvalidOperationException("المبلغ المدفوع يجب أن يكون أكبر من الصفر.");

            if (amountPaidNow > invoice.RemainingAmount)
                throw new InvalidOperationException($"المبلغ المدفوع ({amountPaidNow}) أكبر من المبلغ المتبقي على الفاتورة ({invoice.RemainingAmount}).");

            invoice.PaidAmount += amountPaidNow;
            invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;

            if (invoice.RemainingAmount == 0)
            {
                invoice.PaymentMethod = paymentMethod;
            }
            else
            {
                invoice.PaymentMethod = PaymentMethod.PartiallyPaid;
            }

            _invoiceRepo.Update(invoice);
            await _invoiceRepo.SaveChangesAsync();

            // Update or record Treasury Transaction
            var treasuryTx = await _context.TreasuryTransactions
                .FirstOrDefaultAsync(t => t.SupplierInvoiceId == invoiceId);

            if (treasuryTx != null)
            {
                treasuryTx.PaidAmount = invoice.PaidAmount;
                treasuryTx.RemainingAmount = invoice.RemainingAmount;
                treasuryTx.PaymentMethod = invoice.PaymentMethod;
                treasuryTx.Notes = (treasuryTx.Notes ?? "") + $" | سداد دفعة {amountPaidNow} ج.م بتاريخ {DateTime.Now:yyyy/MM/dd}";
                _treasuryRepo.Update(treasuryTx);
                await _treasuryRepo.SaveChangesAsync();
            }
            else
            {
                var newTreasuryTx = new TreasuryTransaction
                {
                    Date = DateTime.Now,
                    TransactionName = $"سداد مديونية فاتورة #{invoice.InvoiceNumber} ({invoice.Supplier?.Name})",
                    TransactionType = TreasuryTransactionType.Expense,
                    Category = "مواد خام",
                    Amount = invoice.TotalAmount,
                    PaymentMethod = paymentMethod,
                    PaidAmount = amountPaidNow,
                    RemainingAmount = invoice.RemainingAmount,
                    Notes = $"سداد مديونية فاتورة توريد | مورد: {invoice.Supplier?.Name}",
                    SupplierInvoiceId = invoice.Id
                };
                await _treasuryRepo.AddAsync(newTreasuryTx);
                await _treasuryRepo.SaveChangesAsync();
            }

            // Update corresponding Expense records for items
            var invTxs = await _context.InventoryTransactions
                .Where(t => t.SupplierInvoiceId == invoice.Id)
                .ToListAsync();
            var expenseIds = invTxs.Where(t => t.ExpenseId.HasValue).Select(t => t.ExpenseId.Value).ToList();
            if (expenseIds.Any())
            {
                var expenses = await _context.Expenses
                    .Where(e => expenseIds.Contains(e.Id))
                    .ToListAsync();

                decimal paidRatio = invoice.TotalAmount > 0 ? invoice.PaidAmount / invoice.TotalAmount : 0;
                foreach (var exp in expenses)
                {
                    exp.PaidAmount = Math.Round(exp.TotalAmount * paidRatio, 2);
                    exp.RemainingAmount = exp.TotalAmount - exp.PaidAmount;
                    exp.PaymentMethod = invoice.PaymentMethod;
                    _context.Expenses.Update(exp);
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<SupplierInvoice>> GetAllInvoicesAsync()
        {
            return await _context.SupplierInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Items)
                    .ThenInclude(item => item.RawMaterial)
                        .ThenInclude(rm => rm!.MeasurementUnit)
                .Include(i => i.Items)
                    .ThenInclude(item => item.RawMaterial)
                        .ThenInclude(rm => rm!.MaterialType)
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync();
        }

        public async Task<SupplierInvoice?> GetInvoiceByIdAsync(int id)
        {
            return await _context.SupplierInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Items)
                    .ThenInclude(item => item.RawMaterial)
                        .ThenInclude(rm => rm!.MeasurementUnit)
                .Include(i => i.Items)
                    .ThenInclude(item => item.RawMaterial)
                        .ThenInclude(rm => rm!.MaterialType)
                .FirstOrDefaultAsync(i => i.Id == id);
        }


        public async Task<SupplierFinancialSummaryDto> GetFinancialSummaryAsync()
        {
            decimal totalInventoryVal = await _context.RawMaterials.SumAsync(r => r.CurrentQuantity * r.UnitPrice);
            decimal totalIndebtedness = await _context.SupplierInvoices.SumAsync(i => i.RemainingAmount);
            decimal totalPaid = await _context.SupplierInvoices.SumAsync(i => i.PaidAmount);

            return new SupplierFinancialSummaryDto
            {
                TotalInventoryValue = totalInventoryVal,
                TotalIndebtedness = totalIndebtedness,
                TotalPaidToSuppliers = totalPaid
            };
        }
    }
}
