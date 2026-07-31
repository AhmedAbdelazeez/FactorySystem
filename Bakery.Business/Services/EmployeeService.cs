using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.DataAccess.Repositories;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;
using System.Diagnostics.CodeAnalysis;
using Bakery.Business.DTOs;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace Bakery.Business.Services
{
    public interface IEmployeeService
    {
        Task<IEnumerable<EmployeeListItemDto>> GetAllEmployeesAsync(bool? activeOnly = null);
        Task<Employee?> GetEmployeeByIdAsync(int id);
        Task AddEmployeeAsync(Employee employee);
        Task UpdateEmployeeAsync(Employee employee);
        Task DeleteEmployeeAsync(int id);

        Task<string> PaySalaryAsync(int employeeId, PaymentMethod paymentMethod, string? notes = null, DateTime? targetMonth = null);

        Task AddAdvanceAsync(int employeeId, decimal salaryAmount, PaymentMethod paymentMethod, string? notes = null);
    }

    public class EmployeeService : IEmployeeService
    {
        private readonly BakeryDbContext _context;
        private readonly IRepository<Employee> _empRepo;
        private readonly IExpenseService _expenseService;

        private static readonly Dictionary<string, DayOfWeek> DayNameMap = new()
        {
            { "الأحد", DayOfWeek.Sunday },
            { "الاثنين", DayOfWeek.Monday },
            { "الثلاثاء", DayOfWeek.Tuesday },
            { "الأربعاء", DayOfWeek.Wednesday },
            { "الخميس", DayOfWeek.Thursday },
            { "الجمعة", DayOfWeek.Friday },
            { "السبت", DayOfWeek.Saturday }
        };
        public EmployeeService(BakeryDbContext context, IRepository<Employee> empRepo, IExpenseService expenseService)
        {
            _context = context;
            _empRepo = empRepo;
            _expenseService = expenseService;
        }

        public async Task<IEnumerable<EmployeeListItemDto>> GetAllEmployeesAsync(bool? activeOnly = null)
        {
            var query = _context.Employees.AsNoTracking().AsQueryable();

            if (activeOnly.HasValue && activeOnly.Value)
            {
                query = query.Where(e => e.IsActive);
            }


            var employees = await query.OrderBy(e => e.Name).ToListAsync();
            if (!employees.Any()) return Enumerable.Empty<EmployeeListItemDto>();
            var employeeIds = employees.Select(e => e.Id).ToList();

            var lastSalaryDates = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.EmployeeId.HasValue && employeeIds.Contains(e.EmployeeId.Value) && e.Name.StartsWith("راتب العامل"))
                .GroupBy(e => e.EmployeeId!.Value)
                .Select(g => new { EmployeeId = g.Key, LastDate = g.Max(e => (DateTime?)e.Date) })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.LastDate);

            var pendingAdvances = await _context.EmployeeAdvances
                .AsNoTracking()
                .Where(a => employeeIds.Contains(a.EmployeeId) && !a.IsPaid)
                .GroupBy(a => a.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Total = g.Sum(a => a.Amount) })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Total);

            var attendances = await _context.EmployeeAttendances
                .AsNoTracking()
                .Where(a => employeeIds.Contains(a.EmployeeId) && !a.IsPresent)
                .ToListAsync();

            var result = new List<EmployeeListItemDto>();

            foreach (var emp in employees)
            {
                lastSalaryDates.TryGetValue(emp.Id, out var lastSalaryDate);
                pendingAdvances.TryGetValue(emp.Id, out var pendingAdvanceAmount);

                DayOfWeek? dayOff = DayNameMap.TryGetValue(emp.WeeklyDayOff ?? "", out var dow) ? dow : null;

                var empAttendances = attendances.Where(a => a.EmployeeId == emp.Id);
                if (lastSalaryDate.HasValue)
                {
                    empAttendances = empAttendances.Where(a => a.Date.Date > lastSalaryDate.Value.Date);
                }

                int absentDays = dayOff.HasValue
                    ? empAttendances.Count(a => a.Date.DayOfWeek != dayOff.Value)
                    : empAttendances.Count();

                result.Add(new EmployeeListItemDto
                {
                    Id = emp.Id,
                    Name = emp.Name,
                    Age = emp.Age,
                    JobTitle = emp.JobTitle,
                    MonthlySalary = emp.MonthlySalary,
                    WeeklyDayOff = emp.WeeklyDayOff,
                    PhoneNumber = emp.PhoneNumber,
                    Notes = emp.Notes,
                    IsActive = emp.IsActive,
                    AbsentDaysSinceLastSalary = absentDays,
                    PendingAdvanceAmount = pendingAdvanceAmount
                });
            }

            return result;
        }

        public async Task<Employee?> GetEmployeeByIdAsync(int id)
        {
            return await _empRepo.GetByIdAsync(id);
        }

        public async Task AddEmployeeAsync(Employee employee)
        {
            if (employee.Age <= 0)
                throw new InvalidOperationException("عمر العامل يجب أن يكون رقمًا موجبًا أكبر من الصفر.");

            await _empRepo.AddAsync(employee);
            await _empRepo.SaveChangesAsync();
        }

        public async Task UpdateEmployeeAsync(Employee employee)
        {
            var existing = await _empRepo.GetByIdAsync(employee.Id);
            if (existing == null) throw new KeyNotFoundException("العامل غير موجود.");

            if (employee.Age <= 0)
                throw new InvalidOperationException("عمر العامل يجب أن يكون رقمًا موجبًا أكبر من الصفر.");

            existing.Name = employee.Name;
            existing.Age = employee.Age;
            existing.JobTitle = employee.JobTitle;
            existing.MonthlySalary = employee.MonthlySalary;
            existing.WeeklyDayOff = employee.WeeklyDayOff;
            existing.PhoneNumber = employee.PhoneNumber;
            existing.Notes = employee.Notes;
            existing.IsActive = employee.IsActive;

            _empRepo.Update(existing);
            await _empRepo.SaveChangesAsync();
        }

        public async Task DeleteEmployeeAsync(int id)
        {
            var existing = await _empRepo.GetByIdAsync(id);
            if (existing != null)
            {
                _empRepo.Remove(existing);
                await _empRepo.SaveChangesAsync();
            }
        }

        public async Task<string> PaySalaryAsync(int employeeId, PaymentMethod paymentMethod, string? notes = null, DateTime? targetMonth = null)
        {
            var emp = await _empRepo.GetByIdAsync(employeeId);
            if (emp == null) throw new KeyNotFoundException("العامل غير موجود.");

            var payPeriod = targetMonth ?? DateTime.Now;
            var now = DateTime.Now;

            bool salaryPaid = await _context.Expenses
                .AnyAsync(e => e.EmployeeId == employeeId
                       && e.Date.Year == payPeriod.Year
                       && e.Date.Month == payPeriod.Month
                       && e.Name.StartsWith("راتب العامل"));


            if (salaryPaid)
                throw new InvalidOperationException($"تم صرف راتب شهر ({payPeriod:MM/yyyy}) لهذا العامل بالفعل.");

            //int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            decimal dayValue = emp.MonthlySalary / 30m;

            var startDate = new DateTime(payPeriod.Year, payPeriod.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            DayOfWeek? dayOff = DayNameMap.TryGetValue(emp.WeeklyDayOff, out var dow) ? dow : null;

            var attendances = await _context.EmployeeAttendances
               .Where(a => a.EmployeeId == employeeId
                   && !a.IsPresent
                   && a.Date >= startDate
                   && a.Date <= endDate)
               .ToListAsync();

            if (dayOff.HasValue)
            {
                attendances = attendances
                    .Where(a => a.Date.DayOfWeek != dayOff.Value)
                    .ToList();
            }

            int absentDays = attendances.Count;

            decimal absenceDeduction = Math.Round(absentDays * dayValue, 2);

            var unpaidAdvances = await _context.EmployeeAdvances
                .Where(a => a.EmployeeId == employeeId && !a.IsPaid && a.Date <= endDate)
                .ToListAsync();

            decimal totalAdvanceDeduction = unpaidAdvances.Sum(a => a.Amount);

            decimal netSalary = Math.Max(0, emp.MonthlySalary - absenceDeduction - totalAdvanceDeduction);
            netSalary = Math.Round(netSalary, 2);


            var laborCategory = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == "عمالة");
            int categoryId = laborCategory?.Id ?? 1;

            string detailsMessage = $"راتب شهر ({payPeriod:MM/yyyy}) | " +
                              $"الأساسي: {emp.MonthlySalary:N2} ج.م | " +
                              $"خصم غياب ({absentDays} يوم): {absenceDeduction:N2} ج.م | " +
                              $"خصم سلف: {totalAdvanceDeduction:N2} ج.م | " +
                              $"الصافي المصروف: {netSalary:N2} ج.م";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try {
                var expense = new Expense
                {
                    Name = $"راتب العامل: {emp.Name}",
                    ExpenseCategoryId = categoryId,
                    Quantity = 1,
                    UnitPrice = netSalary,
                    TotalAmount = netSalary,
                    Date = DateTime.Now,
                    PaymentMethod = paymentMethod,
                    PaidAmount = netSalary,
                    RemainingAmount = 0,
                    EmployeeId = emp.Id,
                    Notes = string.IsNullOrWhiteSpace(notes) ? detailsMessage : $"{notes} | {detailsMessage}"
                };

                await _expenseService.AddExpenseAsync(expense);

                foreach (var advance in unpaidAdvances)
                {
                    advance.IsPaid = true;
                    advance.PaidDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return detailsMessage;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            } 
        }

        public async Task AddAdvanceAsync(int employeeId, decimal advanceAmount, PaymentMethod paymentMethod, string? notes = null)
        {
            if (advanceAmount <= 0)
                throw new InvalidOperationException("قيمة السلفة يجب أن تكون أكبر من الصفر.");

            var emp = await _empRepo.GetByIdAsync(employeeId);
            if (emp == null) throw new KeyNotFoundException("العامل غير موجود.");

            if (advanceAmount > emp.MonthlySalary)
                throw new InvalidOperationException($"لا يمكن أن تتجاوز السلفة الراتب الشهري ({emp.MonthlySalary:N2} ج.م).");

            var now = DateTime.Now;
            int advancesThisMonth = await _context.EmployeeAdvances
                .Where(a => a.EmployeeId == employeeId && a.Date.Year == now.Year && a.Date.Month == now.Month)
                .CountAsync();

            //if (advancesThisMonth >= 2)
            //    throw new InvalidOperationException($"لا يمكن صرف أكثر من سلفتين للعامل في الشهر الواحد.");

            var advance = new EmployeeAdvance
            {
                EmployeeId = employeeId,
                Amount = advanceAmount,
                Date = now,
                PaymentMethod = paymentMethod,
                Notes = notes,
                IsPaid = false
            };
            await _context.EmployeeAdvances.AddAsync(advance);
            await _context.SaveChangesAsync();

            var laborCategory = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == "عمالة");
            int categoryId = laborCategory?.Id ?? 1;

            var advanceExpense = new Expense
            {
                Name = $"سلفة للعامل: {emp.Name}",
                ExpenseCategoryId = categoryId,
                Quantity = 1,
                UnitPrice = advanceAmount,
                TotalAmount = advanceAmount,
                Date = now,
                PaymentMethod = paymentMethod,
                PaidAmount = advanceAmount,
                RemainingAmount = 0,
                EmployeeId = emp.Id,
                Notes = notes ?? $"سلفة بتاريخ {now:yyyy/MM/dd}"
            };

            await _expenseService.AddExpenseAsync(advanceExpense);
        }
    }
}
