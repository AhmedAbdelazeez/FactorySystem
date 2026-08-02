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
                 .Where(e => e.EmployeeId.HasValue && employeeIds
                 .Contains(e.EmployeeId.Value) && e.Name.StartsWith("راتب العامل"))
                 .Select(e => new { e.EmployeeId, e.Notes, e.Date })
                 .ToListAsync();

            var lastPaidMonthCutoffs = new Dictionary<int, DateTime>();

            foreach (var empId in employeeIds)
            {
                var empSalaries = salaryExpenses.Where(s => s.EmployeeId == empId).ToList();
                if (!empSalaries.Any()) continue;
                
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
            var pendingAdvances = await _context.EmployeeAdvances
                .AsNoTracking()
                .Where(a => employeeIds.Contains(a.EmployeeId) && !a.IsPaid)
                .GroupBy(a => a.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Total = g.Sum(a => a.Amount) })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Total);

            var result = new List<EmployeeListItemDto>();

            

            foreach (var emp in employees)
            {
                bool hasCutoff = lastPaidMonthCutoffs.TryGetValue(emp.Id, out DateTime lastPaidCutoff);
                pendingAdvances.TryGetValue(emp.Id, out var pendingAdvanceAmount);

                DayOfWeek? dayOff = DayNameMap.TryGetValue(emp.WeeklyDayOff ?? "", out var dow) ? dow : null;

                var periodStart = hasCutoff ? lastPaidCutoff.AddDays(1) : emp.StartedDate;
                var periodEnd = DateTime.Today;

                int absentDays = await CountAbsentDaysAsync(emp.Id, dayOff, periodStart, periodEnd);

                

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
                    StartedDate=emp.StartedDate,
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
            var periodStart = lastPaidCutoff?.Date.AddDays(1) ?? emp.StartedDate.Date;
            int absentDaysCount = await CountAbsentDaysAsync(id, dayOff, periodStart, DateTime.Today);

           

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
                SalaryExpenses = salaryExpenses,
                StartedDate = emp.StartedDate
            };
        }

        public async Task AddEmployeeAsync(Employee employee)
        {
            if (employee.Age <= 0)
                throw new InvalidOperationException("عمر العامل يجب أن يكون رقمًا موجبًا أكبر من الصفر.");
            if (employee.MonthlySalary <= 0)
                throw new InvalidOperationException("الراتب الشهري يجب أن يكون أكبر من الصفر.");
            if (employee.StartedDate > DateTime.Today)
                throw new InvalidOperationException("تاريخ بداية العمل لا يمكن أن يكون في المستقبل.");

            await _empRepo.AddAsync(employee);
            await _empRepo.SaveChangesAsync();
        }

        public async Task UpdateEmployeeAsync(Employee employee)
        {
            var existing = await _empRepo.GetByIdAsync(employee.Id);
            if (existing == null) throw new KeyNotFoundException("العامل غير موجود.");

            if (employee.Age <= 0)
                throw new InvalidOperationException("عمر العامل يجب أن يكون رقمًا موجبًا أكبر من الصفر.");
            if (employee.MonthlySalary <= 0)
                throw new InvalidOperationException("الراتب الشهري يجب أن يكون أكبر من الصفر.");
            if (employee.StartedDate > DateTime.Today)
                throw new InvalidOperationException("تاريخ بداية العمل لا يمكن أن يكون في المستقبل.");

            if (existing.StartedDate.Date != employee.StartedDate.Date)
            {
                bool hasSalaryHistory = await _context.Expenses
                    .AnyAsync(e => e.EmployeeId == employee.Id && e.Name.StartsWith("راتب العامل"));

                if (hasSalaryHistory)
                    throw new InvalidOperationException("لا يمكن تعديل تاريخ بداية العمل لأن هذا الموظف لديه رواتب مصروفة بالفعل.");
            }

            existing.Name = employee.Name;
            existing.Age = employee.Age;
            existing.JobTitle = employee.JobTitle;
            existing.MonthlySalary = employee.MonthlySalary;
            existing.WeeklyDayOff = employee.WeeklyDayOff;
            existing.PhoneNumber = employee.PhoneNumber;
            existing.Notes = employee.Notes;
            existing.IsActive = employee.IsActive;
            existing.StartedDate = employee.StartedDate;

            _empRepo.Update(existing);
            await _empRepo.SaveChangesAsync();
        }

        public async Task DeleteEmployeeAsync(int id)
        {
            var existing = await _empRepo.GetByIdAsync(id);
            if (existing == null) throw new KeyNotFoundException("العامل غير موجود.");
            
            _empRepo.Remove(existing);
            await _empRepo.SaveChangesAsync();
            
        }

        public async Task<string> PaySalaryAsync(int employeeId, PaymentMethod paymentMethod, string? notes = null, DateTime? targetMonth = null)
        {
            var emp = await _empRepo.GetByIdAsync(employeeId);
            if (emp == null) throw new KeyNotFoundException("العامل غير موجود.");
            
            if (!emp.IsActive) throw new InvalidOperationException("لا يمكن صرف راتب لموظف غير نشط.");
            
            if (emp.MonthlySalary <= 0)
                throw new InvalidOperationException("الراتب الشهري لهذا الموظف غير صحيح، يرجى تحديثه أولاً.");

            var payPeriod = targetMonth ?? DateTime.Now;

            var requestedMonth = new DateTime(payPeriod.Year, payPeriod.Month, 1);
            var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (requestedMonth > currentMonth)
                throw new InvalidOperationException("لا يمكن صرف راتب لشهر لم يبدأ بعد.");


            string targetMonthFormatted = payPeriod.ToString("MM/yyyy");

            var startDate = new DateTime(payPeriod.Year, payPeriod.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var empSalaryNotes = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.Name.StartsWith("راتب العامل"))
                .Select(e => e.Notes)
                .ToListAsync();

            bool salaryPaid = empSalaryNotes.Any(n => n != null && n.Contains($"({targetMonthFormatted})"));


            if (salaryPaid)
                throw new InvalidOperationException($"تم صرف راتب شهر ({targetMonthFormatted}) لهذا العامل بالفعل.");


            var firstPossibleMonth = await GetFirstUnpaidMonthAsync(employeeId);
            if (firstPossibleMonth.HasValue && requestedMonth > firstPossibleMonth.Value)
            {
                throw new InvalidOperationException(
                    $"لا يمكن صرف راتب شهر ({payPeriod:MM/yyyy}) قبل صرف راتب شهر ({firstPossibleMonth.Value:MM/yyyy}) أولاً.");
            }

            if (emp.StartedDate.Date > endDate)
                throw new InvalidOperationException("تاريخ تعيين الموظف أحدث من الشهر المطلوب صرفه.");

            int totalDaysInMonth = DateTime.DaysInMonth(payPeriod.Year, payPeriod.Month);
            decimal baseSalaryForMonth = emp.MonthlySalary;


            if (emp.StartedDate.Year == payPeriod.Year && emp.StartedDate.Month == payPeriod.Month)
            {
                int workedDays = (endDate - emp.StartedDate.Date).Days + 1;
                if (workedDays < totalDaysInMonth)
                {
                    decimal dailyRateForMonth = emp.MonthlySalary / totalDaysInMonth;
                    baseSalaryForMonth = Math.Round(dailyRateForMonth * workedDays, 2);
                }
            }

            decimal dayValue = emp.MonthlySalary / 30m;
            DayOfWeek? dayOff = DayNameMap.TryGetValue(emp.WeeklyDayOff ?? "", out var dow) ? dow : null;

            var absencePeriodStart = emp.StartedDate.Date > startDate ? emp.StartedDate.Date : startDate;
         
            int absentDays = await CountAbsentDaysAsync(employeeId, dayOff, absencePeriodStart, endDate);
            decimal absenceDeduction = Math.Round(absentDays * dayValue, 2);


            var unpaidAdvances = await _context.EmployeeAdvances
            .Where(a => a.EmployeeId == employeeId && !a.IsPaid && a.Date <= endDate)
            .ToListAsync();

            decimal totalAdvanceDeduction = unpaidAdvances.Sum(a => a.Amount);


            decimal netSalary = Math.Max(0, baseSalaryForMonth - absenceDeduction - totalAdvanceDeduction);
            netSalary = Math.Round(netSalary, 2);

            var laborCategory = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == "عمالة");
            int categoryId = laborCategory?.Id ?? 1;

            // تجهيز تفاصيل نص الرسالة
            string detailsMessage = $"راتب شهر ({payPeriod:MM/yyyy}) | " +
                               $"الأساسي: {baseSalaryForMonth:N2} ج.م | " +
                               $"خصم غياب ({absentDays} يوم): {absenceDeduction:N2} ج.م | " +
                               $"خصم سلف: {totalAdvanceDeduction:N2} ج.م | " +
                               $"الصافي المصروف: {netSalary:N2} ج.م";

            if (netSalary == 0)
                detailsMessage += " | ⚠️ تنبيه: الصافي المستحق صفر بسبب الخصومات (غياب/سلف تغطي كامل الراتب).";


            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {

                // ب. تسجيل المصروف
                var salaryExpense = new Expense
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
                await _expenseService.AddExpenseAsync(salaryExpense);
                // await _context.SaveChangesAsync();


                // أ. تسوية وإغلاق السُلف المعلقة
                foreach (var advance in unpaidAdvances)
                {
                    advance.IsPaid = true;
                    advance.PaidDate = DateTime.Now;
                }

                // ج. الخصم من الخزينة
                //var treasuryTransaction = new TreasuryTransaction
                //{
                //    Date = DateTime.Now,
                //    Amount = netSalary,
                //    Type = TransactionType.Expense,
                //    ExpenseId = salaryExpense.Id,
                //    Notes = $"صرف راتب للعامل: {emp.Name} - شهر ({payPeriod:MM/yyyy})"
                //};
                //_context.TreasuryTransactions.Add(treasuryTransaction);

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

            if (!emp.IsActive)
                throw new InvalidOperationException("لا يمكن صرف سلفة لموظف غير نشط.");

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
            var emp=await _empRepo.GetByIdAsync(employeeId);
            if (emp == null) return null;


            var startMonth = new DateTime(emp.StartedDate.Year, emp.StartedDate.Month, 1);

            var empSalaryNotes = await _context.Expenses
               .AsNoTracking()
               .Where(e => e.EmployeeId == employeeId && e.Name.StartsWith("راتب العامل"))
               .Select(e => e.Notes)
               .ToListAsync();

            var cursor = startMonth;
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


        private async Task<int> CountAbsentDaysAsync(int employeeId, DayOfWeek? dayOff, DateTime periodStart, DateTime periodEnd)
        {
            var effectiveEnd = periodEnd > DateTime.Today ? DateTime.Today : periodEnd;
            if (effectiveEnd < periodStart) return 0;

            var presentDates = await _context.EmployeeAttendances
                .Where(a => a.EmployeeId == employeeId
                    && a.IsPresent
                    && a.Date >= periodStart
                    && a.Date <= effectiveEnd)
                .Select(a => a.Date.Date)
                .ToListAsync();

            var presentSet = new HashSet<DateTime>(presentDates);

            int absentCount = 0;
            for (var d = periodStart.Date; d <= effectiveEnd.Date; d = d.AddDays(1))
            {
                if (dayOff.HasValue && d.DayOfWeek == dayOff.Value)
                    continue;

                if (!presentSet.Contains(d))
                    absentCount++;
            }

            return absentCount;
        }
    }
}
