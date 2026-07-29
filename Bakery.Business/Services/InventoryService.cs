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
        Task AddRawMaterialAsync(RawMaterial material);
        Task UpdateRawMaterialAsync(RawMaterial material);
        Task DeleteRawMaterialAsync(int id);
        Task<decimal> GetTotalInventoryValueAsync();
        Task AddStockAsync(int rawMaterialId, decimal quantity, decimal unitPrice, string? notes = null, int? expenseId = null);
        Task DeductStockAsync(int rawMaterialId, decimal quantity, int productionOrderId, string? notes = null);
        Task<IEnumerable<InventoryTransaction>> GetTransactionsAsync(int? rawMaterialId = null);
        Task<bool> MaterialTypeExistsAsync(int materialTypeId, int? excludeId = null);


    }

    public class InventoryService : IInventoryService
    {
        private readonly BakeryDbContext _context;
        private readonly IRepository<RawMaterial> _materialRepo;
        private readonly IRepository<InventoryTransaction> _transactionRepo;

        public InventoryService(BakeryDbContext context, IRepository<RawMaterial> materialRepo, IRepository<InventoryTransaction> transactionRepo)
        {
            _context = context;
            _materialRepo = materialRepo;
            _transactionRepo = transactionRepo;
        }

        public async Task<IEnumerable<RawMaterial>> GetAllRawMaterialsAsync()
        {
            return await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .Include(r => r.MaterialType)
                .OrderBy(r => r.MaterialType!.Name)
                .ToListAsync();
        }

        public async Task<RawMaterial?> GetRawMaterialByIdAsync(int id)
        {
            return await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .Include(r => r.MaterialType)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task AddRawMaterialAsync(RawMaterial material)
        {
            if (await MaterialTypeExistsAsync(material.MaterialTypeId))
            {
                var type = await _context.MaterialTypes.FindAsync(material.MaterialTypeId);
                throw new InvalidOperationException($"المادة الخام '{type?.Name}' موجودة بالفعل في المخزن، يمكنك توريد كمية جديدة لها بدلاً من الإضافة.");
            }
            if (material.CurrentQuantity < 0)
                throw new InvalidOperationException("لا يمكن أن تكون كمية المخزون بالسالب.");

            material.TotalValue = material.CurrentQuantity * material.UnitPrice;
            material.LastUpdatedDate = DateTime.Now;
            await _materialRepo.AddAsync(material);
            await _materialRepo.SaveChangesAsync();
        }

        public async Task UpdateRawMaterialAsync(RawMaterial material)
        {
            var existing = await _materialRepo.GetByIdAsync(material.Id);
            if (existing == null) throw new KeyNotFoundException("المادة الخام غير موجودة.");

            if (material.CurrentQuantity < 0)
                throw new InvalidOperationException("لا يمكن أن تكون كمية المخزون بالسالب.");

            if (material.UnitPrice < 0)
                throw new InvalidOperationException("لا يمكن أن يكون السعر بالسالب.");

            // نوع المادة (MaterialTypeId) ثابت ومايتغيرش بعد الإضافة
            existing.MeasurementUnitId = material.MeasurementUnitId;
            existing.CurrentQuantity = material.CurrentQuantity;
            existing.UnitPrice = material.UnitPrice;
            existing.TotalValue = existing.CurrentQuantity * existing.UnitPrice;
            existing.LastUpdatedDate = DateTime.Now;

            _materialRepo.Update(existing);
            await _materialRepo.SaveChangesAsync();
        }

        public async Task DeleteRawMaterialAsync(int id)
        {
            var existing = await _materialRepo.GetByIdAsync(id);
            if (existing == null)
                return;

            bool hasTransactions = await _context.InventoryTransactions
                .AnyAsync(t => t.RawMaterialId == id);

            if (hasTransactions)

                throw new InvalidOperationException("لا يمكن حذف هذه المادة لوجود حركات مسجلة عليها في سجل المخزن. يمكنك بدلاً من ذلك تصفير الكمية.");

            bool usedInRecipes = await _context.ProductionRecipeItems
                .AnyAsync(r => r.RawMaterialId == id);
            if(usedInRecipes)
                throw new InvalidOperationException("لا يمكن حذف هذه المادة لأنهامستخدمه فى وصفة انتاج . قم بأزالتها من الوصفه أولا.");

            _materialRepo.Remove(existing);
            await _materialRepo.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalInventoryValueAsync()
        {
            return await _context.RawMaterials.SumAsync(r => r.CurrentQuantity * r.UnitPrice);
        }

        public async Task AddStockAsync(int rawMaterialId, decimal quantity, decimal unitPrice, string? notes = null, int? expenseId = null)
        {
            

            if (quantity <= 0) throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

            var material = await _materialRepo.GetByIdAsync(rawMaterialId);
            if (material == null) throw new KeyNotFoundException("المادة الخام غير موجودة.");

            material.CurrentQuantity += quantity;
            material.UnitPrice = unitPrice > 0 ? unitPrice : material.UnitPrice;
            material.TotalValue = material.CurrentQuantity * material.UnitPrice;
            material.LastUpdatedDate = DateTime.Now;

            _materialRepo.Update(material);

            var transaction = new InventoryTransaction
            {
                RawMaterialId = rawMaterialId,
                TransactionType = TransactionType.Purchase,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalAmount = quantity * unitPrice,
                TransactionDate = DateTime.Now,
                ExpenseId = expenseId,
                Notes = notes ?? "إضافة / توريد مخزون"
            };

            await _transactionRepo.AddAsync(transaction);
            await _context.SaveChangesAsync();
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
                .AsQueryable();

            if (rawMaterialId.HasValue)
            {
                query = query.Where(t => t.RawMaterialId == rawMaterialId.Value);
            }

            return await query.OrderByDescending(t => t.TransactionDate).ToListAsync();
        }

        public async Task<bool> MaterialTypeExistsAsync(int materialTypeId, int? excludeId = null)
        {
            var query = _context.RawMaterials.Where(m => m.MaterialTypeId == materialTypeId);

            if (excludeId.HasValue)
                query = query.Where(m => m.Id != excludeId.Value);

            var result = await query.AnyAsync();
            return result;
        }
    }
}
