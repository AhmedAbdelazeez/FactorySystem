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

        Task<bool> MaterialNameExistsAsync(string name, int? excludeId = null);
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
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<RawMaterial?> GetRawMaterialByIdAsync(int id)
        {
            return await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task AddRawMaterialAsync(RawMaterial material)
        {
            if(await MaterialNameExistsAsync(material.Name))
                throw new InvalidOperationException($"الماده الخام '{material.Name}' موجودة بالفعل فى المخزن يمكنك توريد كميه جديده.");

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

            if(await MaterialNameExistsAsync(material.Name,excludeId:material.Id))
                throw new InvalidOperationException($"يوجد مادة خام بنفس الاسم.'{material.Name}'");

            if (material.CurrentQuantity < 0)
                throw new InvalidOperationException("لا يمكن أن تكون كمية المخزون بالسالب.");

            existing.Name = material.Name;
            existing.MeasurementUnitId = material.MeasurementUnitId;
            existing.CurrentQuantity = material.CurrentQuantity;
            existing.UnitPrice = material.UnitPrice;
            existing.TotalValue = material.CurrentQuantity * material.UnitPrice;
            existing.LastUpdatedDate = DateTime.Now;

            _materialRepo.Update(existing);
            await _materialRepo.SaveChangesAsync();
        }

        public async Task DeleteRawMaterialAsync(int id)
        {
            var existing = await _materialRepo.GetByIdAsync(id);
            if (existing != null)
            {
                _materialRepo.Remove(existing);
                await _materialRepo.SaveChangesAsync();
            }
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

            var material = await _materialRepo.GetByIdAsync(rawMaterialId);
            if (material == null) throw new KeyNotFoundException("المادة الخام غير موجودة.");

            if (material.CurrentQuantity < quantity)
            {
                throw new InvalidOperationException($"الكمية المتاحة من المادة الخام ({material.Name}) غير كافية. المتاح: {material.CurrentQuantity} ، المطلوب: {quantity}");
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
                .AsQueryable();

            if (rawMaterialId.HasValue)
            {
                query = query.Where(t => t.RawMaterialId == rawMaterialId.Value);
            }

            return await query.OrderByDescending(t => t.TransactionDate).ToListAsync();
        }

        public async Task<bool> MaterialNameExistsAsync(string name, int? excludeId = null)
        {
            var normName = name.Trim().ToLower();

            var query = _context.RawMaterials
                .Where(m=>m.Name.Trim().ToLower()==normName);

            if(excludeId.HasValue)
            {
                query = query.Where(m => m.Id != excludeId.Value);
            }
            var result = await query.AnyAsync();

            return result;
        }
    }
}
