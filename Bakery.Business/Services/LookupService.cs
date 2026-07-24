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
        Task AddMeasurementUnitAsync(string name);
        List<string> GetWeekdays();
        List<string> GetDefaultJobTitles();
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

        public async Task AddMeasurementUnitAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!await _context.MeasurementUnits.AnyAsync(u => u.Name == name.Trim()))
            {
                await _context.MeasurementUnits.AddAsync(new MeasurementUnit { Name = name.Trim() });
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
    }
}
