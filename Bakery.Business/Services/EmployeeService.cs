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
        Task<EmployeeDetailsDto?> GetEmployeeByIdAsync(int id);
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

            var salaryExpenses = await _context.Expenses
        .AsNoTracking()
        .Where(e => e.EmployeeId.HasValue && employeeIds.Contains(e.EmployeeId.Value) && e.Name.StartsWith("راتب العامل"))
        .Select(e => new { e.EmployeeId, e.Notes, e.Date })
        .ToListAsync();

            var lastPaidMonthCutoffs = new Dictionary<int, DateTime>();

            foreach (var empId in employeeIds)
            {
                var empSalaries = salaryExpenses.Where(s => s.EmployeeId == empId).ToList();
                if (empSalaries.Any())
                {
                    DateTime maxPaidMonthEnd = DateTime.MinValue;

                    foreach (var salary in empSalaries)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(salary.Notes ?? "", @"\((?<month>\d{2})/(?<year>\d{4})\)");

                        DateTime monthEnd;
                        if (match.Success)
                        {
                            int m = int.Parse(match.Groups["month"].Value);
                            int y = int.Parse(match.Groups["year"].Value);
                            monthEnd = new DateTime(y, m, DateTime.DaysInMonth(y, m));
                        }
                        else
                        {
                            monthEnd = new DateTime(salary.Date.Year, salary.Date.Month, DateTime.DaysInMonth(salary.Date.Year, salary.Date.Month));
                        }

                        if (monthEnd > maxPaidMonthEnd)
                        {
                            maxPaidMonthEnd = monthEnd;
                        }
                    }

                    if (maxPaidMonthEnd != DateTime.MinValue)
                    {
                        lastPaidMonthCutoffs[empId] = maxPaidMonthEnd;
                    }
                }
            }
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
                bool hasCutoff = lastPaidMonthCutoffs.TryGetValue(emp.Id, out DateTime lastPaidCutoff);
                pendingAdvances.TryGetValue(emp.Id, out var pendingAdvanceAmount);

                DayOfWeek? dayOff = DayNameMap.TryGetValue(emp.WeeklyDayOff ?? "", out var dow) ? dow : null;

                var empAttendances = attendances.Where(a => a.EmployeeId == emp.Id);

                if (hasCutoff)
                {
                    empAttendances = empAttendances.Where(a => a.Date.Date > lastPaidCutoff.Date);
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

        public async Task<EmployeeDetailsDto?> GetEmployeeByIdAsync(int id)
        {
            var emp = await _empRepo.GetByIdAsync(id);
            if (emp == null) return null;

            // 1. جلب السُلف
            var advances = await _context.EmployeeAdvances
                .AsNoTracking()
                .Where(a => a.EmployeeId == id)
                .OrderByDescending(a => a.Date)
                .Select(a => new AdvanceItemDto
                {
                    Id = a.Id,
                    Amount = a.Amount,
                    Date = a.Date,
                    IsPaid = a.IsPaid,
                    PaidDate = a.PaidDate,
                    Notes = a.Notes
                })
                .ToListAsync();

            // 2. جلب سجل المرتبات المصروفة
            var salaryExpenses = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.EmployeeId == id && e.Name.StartsWith("راتب العامل"))
                .OrderByDescending(e => e.Date)
                .Select(e => new SalaryExpenseItemDto
                {
                    Id = e.Id,
                    Amount = e.TotalAmount,
                    Date = e.Date,
                    Notes = e.Notes
                })
                .ToListAsync();

            // 3. تحديد آخر شهر مدفوع لحساب الغياب المتبقي
            DateTime? lastPaidCutoff = null;
            foreach (var salary in salaryExpenses)
            {
                var match = System.Text.RegularExpressions.Regex.Match(salary.Notes ?? "", @"\((?<month>\d{2})/(?<year>\d{4})\)");
                if (match.Success)
                {
                    int m = int.Parse(match.Groups["month"].Value);
                    int y = int.Parse(match.Groups["year"].Value);
                    var monthEnd = new DateTime(y, m, DateTime.DaysInMonth(y, m));
                    if (!lastPaidCutoff.HasValue || monthEnd > lastPaidCutoff.Value)
                    {
                        lastPaidCutoff = monthEnd;
                    }
                }
            }

            // 4. جلب سجلات الغياب
            var attendances = await _context.EmployeeAttendances
                .AsNoTracking()
                .Where(a => a.EmployeeId == id && !a.IsPresent)
                .OrderByDescending(a => a.Date)
                .Select(a => new AttendanceItemDto
                {
                    Date = a.Date,
                    IsPresent = a.IsPresent,
                    Notes = a.Notes
                })
                .ToListAsync();

            DayOfWeek? dayOff = DayNameMap.TryGetValue(emp.WeeklyDayOff ?? "", out var dow) ? dow : null;

            var pendingAttendances = attendances.AsEnumerable();
            if (lastPaidCutoff.HasValue)
            {
                pendingAttendances = pendingAttendances.Where(a => a.Date.Date > lastPaidCutoff.Value.Date);
            }

            int absentDaysCount = dayOff.HasValue
                ? pendingAttendances.Count(a => a.Date.DayOfWeek != dayOff.Value)
                : pendingAttendances.Count();

            return new EmployeeDetailsDto
            {
                Id = emp.Id,
                Name = emp.Name,
                JobTitle = emp.JobTitle,
                Age = emp.Age,
                MonthlySalary = emp.MonthlySalary,
                WeeklyDayOff = emp.WeeklyDayOff,
                PhoneNumber = emp.PhoneNumber,
                Notes = emp.Notes,
                IsActive = emp.IsActive,
                PendingAdvanceAmount = advances.Where(a => !a.IsPaid).Sum(a => a.Amount),
                AbsentDaysCount = absentDaysCount,
                Attendances = attendances,
                Advances = advances,
                SalaryExpenses = salaryExpenses
            };
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

            string targetMonthFormatted = payPeriod.ToString("MM/yyyy");

            var empSalaries = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.Name.StartsWith("راتب العامل"))
                .Select(e => e.Notes)
                .ToListAsync();

            bool salaryPaid = empSalaries
                .Any(n => n != null && n.Contains($"({targetMonthFormatted})"));


            if (salaryPaid)
                throw new InvalidOperationException($"تم صرف راتب شهر ({targetMonthFormatted:MM/yyyy}) لهذا العامل بالفعل.");


            var firstPossibleMonth = await GetFirstUnpaidMonthAsync(employeeId);

            if (firstPossibleMonth.HasValue)
            {
                var requestedMonthStart = new DateTime(payPeriod.Year, payPeriod.Month, 1);

                if (requestedMonthStart > firstPossibleMonth.Value)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن صرف راتب شهر ({payPeriod:MM/yyyy}) قبل صرف راتب شهر ({firstPossibleMonth.Value:MM/yyyy}) أولاً.");
                }
            }

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


        private async Task<DateTime?> GetFirstUnpaidMonthAsync(int employeeId)
        {
            // أول شهر لينا فيه سجل حضور للموظف (يعني بداية عمله الفعلية في النظام)
            var firstAttendanceDate = await _context.EmployeeAttendances
                .Where(a => a.EmployeeId == employeeId)
                .OrderBy(a => a.Date)
                .Select(a => (DateTime?)a.Date)
                .FirstOrDefaultAsync();

            if (!firstAttendanceDate.HasValue)
                return null; // مفيش أي سجلات حضور خالص، مفيش قيد يمنع الصرف

            var empSalaryNotes = await _context.Expenses
        .AsNoTracking()
        .Where(e => e.EmployeeId == employeeId && e.Name.StartsWith("راتب العامل"))
        .Select(e => e.Notes)
        .ToListAsync();

            var cursor = new DateTime(firstAttendanceDate.Value.Year, firstAttendanceDate.Value.Month, 1);
            var thisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            while (cursor <= thisMonth)
            {
                string monthFormatted = cursor.ToString("MM/yyyy");
                bool paidThisMonth = empSalaryNotes
                    .Any(n => n != null && n.Contains($"({monthFormatted})"));

                if (!paidThisMonth)
                    return cursor; // ده أول شهر معلّق لسه مصرفش

                cursor = cursor.AddMonths(1);
            }

            return null; // كل الشهور اتصرفت لحد الشهر الحالي
        }
    }
}
