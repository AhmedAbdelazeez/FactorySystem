using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.Domain.Entities;

namespace Bakery.Business.Services
{
    public interface ILookupService
    {
        Task<IEnumerable<ExpenseCategory>> GetExpenseCategoriesAsync();
        Task AddExpenseCategoryAsync(string name);
        Task<IEnumerable<MeasurementUnit>> GetMeasurementUnitsAsync();
        Task<MeasurementUnit?> GetMeasurementUnitByIdAsync(int id);
        Task UpdateMeasurementUnitAsync(MeasurementUnit unit);
        Task DeleteMeasurementUnitAsync(int id);
        Task<IEnumerable<MaterialType>> GetMaterialTypesAsync();
        Task<MaterialType> GetMatrialByIdAsync(int id);
        Task UpdateMaterialAsync(MaterialType mType);
        Task DeleteMaterialAsync(int id);
        Task AddMeasurementUnitAsync(string name);
        Task AddMaterialTypeAsync(string name);
        List<string> GetWeekdays();
        List<string> GetDefaultJobTitles();
        Task SyncRawMaterialsAsync();
    }

    public class LookupService : ILookupService
    {
        private readonly BakeryDbContext _context;

        public LookupService(BakeryDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ExpenseCategory>> GetExpenseCategoriesAsync()
        {
            return await _context.ExpenseCategories.OrderBy(c => c.Id).ToListAsync();
        }

        public async Task AddExpenseCategoryAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!await _context.ExpenseCategories.AnyAsync(c => c.Name == name.Trim()))
            {
                await _context.ExpenseCategories.AddAsync(new ExpenseCategory { Name = name.Trim(), IsSystem = false });
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<MeasurementUnit>> GetMeasurementUnitsAsync()
        {
            return await _context.MeasurementUnits.OrderBy(u => u.Id).ToListAsync();
        }

        public async Task<IEnumerable<MaterialType>> GetMaterialTypesAsync()
        {
            return await _context.MaterialTypes.OrderBy(t => t.Id).ToListAsync();
        }

        public async Task AddMeasurementUnitAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!await _context.MeasurementUnits.AnyAsync(u => u.Name == name.Trim()))
            {
                await _context.MeasurementUnits.AddAsync(new MeasurementUnit { Name = name.Trim() });
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddMaterialTypeAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!await _context.MaterialTypes.AnyAsync( m=>m.Name == name.Trim()))
            {
                await _context.MaterialTypes.AddAsync(new MaterialType { Name = name.Trim() });
                await _context.SaveChangesAsync();
                await SyncRawMaterialsAsync();
            }
        }

        public async Task SyncRawMaterialsAsync()
        {
            var materialTypesWithoutRawMaterial = await _context.MaterialTypes
                .Where(mt => !_context.RawMaterials.Any(rm => rm.MaterialTypeId == mt.Id))
                .ToListAsync();

            if (materialTypesWithoutRawMaterial.Any())
            {
                var unit = await _context.MeasurementUnits.FirstOrDefaultAsync();
                if (unit == null)
                {
                    unit = new MeasurementUnit { Name = "وحدة" };
                    await _context.MeasurementUnits.AddAsync(unit);
                    await _context.SaveChangesAsync();
                }

                foreach (var mt in materialTypesWithoutRawMaterial)
                {
                    var rawMat = new RawMaterial
                    {
                        Name = "",
                        MaterialTypeId = mt.Id,
                        MeasurementUnitId = unit.Id,
                        CurrentQuantity = 0,
                        UnitPrice = 0,
                        TotalValue = 0,
                        LastUpdatedDate = DateTime.Now
                    };
                    await _context.RawMaterials.AddAsync(rawMat);
                }
                await _context.SaveChangesAsync();
            }
        }

        public List<string> GetWeekdays()
        {
            return new List<string> { "السبت", "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة" };
        }

        public List<string> GetDefaultJobTitles()
        {
            return new List<string> { "عجان", "فرّان", "عامل تعبئة وتغليف", "مشرف إنتاج", "سائق نقل", "عامل نظافة", "مدير المخبز" };
        }

        public async Task<MaterialType?> GetMatrialByIdAsync(int id)
        {
            return await _context.MaterialTypes.FirstOrDefaultAsync(m => m.Id == id);
            
        }

        public async Task UpdateMaterialAsync(MaterialType mType)
        {
            var existing = await GetMatrialByIdAsync(mType.Id);

            if (existing == null)
                throw new KeyNotFoundException("الماده غير موجوده.");
            existing.Name = mType.Name;
            _context.MaterialTypes.Update(existing);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMaterialAsync(int id)
        {
            var mat = await _context.MaterialTypes.FindAsync(id);
            if (mat == null) return;

            var rawMaterialsOfThisType = await _context.RawMaterials
                .Where(r => r.MaterialTypeId == id)
                .ToListAsync();

            var rawMaterialIds = rawMaterialsOfThisType.Select(r => r.Id).ToList();

            if (rawMaterialIds.Any())
            {
                bool hasTransactions = await _context.InventoryTransactions.AnyAsync(t => rawMaterialIds.Contains(t.RawMaterialId));
                bool hasRecipeItems = await _context.ProductionRecipeItems.AnyAsync(ri => rawMaterialIds.Contains(ri.RawMaterialId));
                bool hasSupplierMaterials = await _context.SupplierRawMaterials.AnyAsync(sm => rawMaterialIds.Contains(sm.RawMaterialId));

                if (hasTransactions || hasRecipeItems || hasSupplierMaterials)
                {
                    throw new InvalidOperationException("لا يمكن حذف هذه المادة الخام لأنها مستخدمة في عمليات مخزنية، أو وصفات إنتاج، أو موردين.");
                }

                _context.RawMaterials.RemoveRange(rawMaterialsOfThisType);
            }

            _context.MaterialTypes.Remove(mat);
            await _context.SaveChangesAsync();
        }

        public async Task<MeasurementUnit?> GetMeasurementUnitByIdAsync(int id)
        {
            return await _context.MeasurementUnits.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task UpdateMeasurementUnitAsync(MeasurementUnit unit)
        {
            var existing = await GetMeasurementUnitByIdAsync(unit.Id);

            if (existing == null)
                throw new KeyNotFoundException("وحدة القياس غير موجودة.");

            existing.Name = unit.Name;
            _context.MeasurementUnits.Update(existing);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMeasurementUnitAsync(int id)
        {
            var unit = await _context.MeasurementUnits.FindAsync(id);
            if (unit == null)
                throw new KeyNotFoundException("وحدة القياس غير موجودة.");

            // التحقق من أن وحدة القياس غير مرتبطة بأي مواد خام في المخزن
            var hasRelatedRecords = await _context.RawMaterials.AnyAsync(r => r.MeasurementUnitId == id);

            if (hasRelatedRecords)
                throw new InvalidOperationException("لا يمكن حذف وحدة القياس هذه لأنها مستخدمة في أصناف مخزنية قائمة.");

            _context.MeasurementUnits.Remove(unit);
            await _context.SaveChangesAsync();
        }
    }
}
