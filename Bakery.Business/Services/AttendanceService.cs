using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.Domain.Entities;

namespace Bakery.Business.Services
{
    public interface IAttendanceService
    {
        Task<IEnumerable<EmployeeAttendance>> GetDailyAttendanceAsync(DateTime date);
        Task ConfirmAttendanceAsync(int employeeId, DateTime date, bool isPresent, string? notes = null);
        Task SaveBulkAttendanceAsync(DateTime date, List<int> presentEmployeeIds);
        Task DeleteAttendanceAsync(int id);
    }

    public class AttendanceService : IAttendanceService
    {
        private readonly BakeryDbContext _context;
        private static bool IsEmployeeOffDay(string? weeklyDayOff, DayOfWeek currentDayOfWeek)
        {
            if (string.IsNullOrWhiteSpace(weeklyDayOff)) return false;

            var dayName = weeklyDayOff.Trim();

            return dayName switch
            {
                "السبت" => currentDayOfWeek == DayOfWeek.Saturday,
                "الأحد" or "الاحد" => currentDayOfWeek == DayOfWeek.Sunday,
                "الإثنين" or "الاثنين" => currentDayOfWeek == DayOfWeek.Monday,
                "الثلاثاء" => currentDayOfWeek == DayOfWeek.Tuesday,
                "الأربعاء" or "الاربعاء" => currentDayOfWeek == DayOfWeek.Wednesday,
                "الخميس" => currentDayOfWeek == DayOfWeek.Thursday,
                "الجمعة" => currentDayOfWeek == DayOfWeek.Friday,
                _ => false
            };
        }
        public AttendanceService(BakeryDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<EmployeeAttendance>> GetDailyAttendanceAsync(DateTime date)
        {
            var targetDate = date.Date;
            var attendances = await _context.EmployeeAttendances
                .Include(a => a.Employee)
                .Where(a => a.Date == targetDate)
                .ToListAsync();

            var activeEmployees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .ToListAsync();

            // Ensure every active employee has a record for targetDate
            var list = new List<EmployeeAttendance>();
            foreach (var emp in activeEmployees)
            {
                var existing = attendances.FirstOrDefault(a => a.EmployeeId == emp.Id);
                if (existing != null)
                {
                    list.Add(existing);
                }
                else
                {
                    list.Add(new EmployeeAttendance
                    {
                        EmployeeId = emp.Id,
                        Employee = emp,
                        Date = targetDate,
                        IsPresent = false,
                        Notes = null
                    });
                }
            }

            return list;
        }

        public async Task ConfirmAttendanceAsync(int employeeId, DateTime date, bool isPresent, string? notes = null)
        {
            var targetDate = date.Date;
            var emp = await _context.Employees.FindAsync(employeeId);
            if (emp == null) throw new KeyNotFoundException("الموظف غير موجود.");

            if (IsEmployeeOffDay(emp.WeeklyDayOff, targetDate.DayOfWeek))
            {
                throw new InvalidOperationException($"لا يمكن تسجيل حضور أو غياب للموظف ({emp.Name}) لأن يوم ({targetDate.ToString("dddd")}) هو يوم إجازته الأسبوعية.");
            }
            var attendance = await _context.EmployeeAttendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == targetDate);

            if (attendance == null)
            {
                attendance = new EmployeeAttendance
                {
                    EmployeeId = employeeId,
                    Date = targetDate,
                    IsPresent = isPresent,
                    CheckInTime = isPresent ? DateTime.Now : null,
                    Notes = notes,
                    CreatedAt = DateTime.Now
                };
                await _context.EmployeeAttendances.AddAsync(attendance);
            }
            else
            {
                attendance.IsPresent = isPresent;
                attendance.CheckInTime = isPresent ? (attendance.CheckInTime ?? DateTime.Now) : null;
                if (!string.IsNullOrEmpty(notes))
                    attendance.Notes = notes;
                _context.EmployeeAttendances.Update(attendance);
            }

            await _context.SaveChangesAsync();
        }

        public async Task SaveBulkAttendanceAsync(DateTime date, List<int> presentEmployeeIds)
        {
            var targetDate = date.Date;
            var activeEmployees = await _context.Employees.Where(e => e.IsActive).ToListAsync();
            var existingAttendances = await _context.EmployeeAttendances
                .Where(a => a.Date == targetDate)
                .ToListAsync();

            presentEmployeeIds ??= new List<int>();

            foreach (var emp in activeEmployees)
            {
                if (IsEmployeeOffDay(emp.WeeklyDayOff, targetDate.DayOfWeek))
                {
                    continue;
                }
                bool isPresent = presentEmployeeIds.Contains(emp.Id);
                var existing = existingAttendances.FirstOrDefault(a => a.EmployeeId == emp.Id);

                if (existing == null)
                {
                    var att = new EmployeeAttendance
                    {
                        EmployeeId = emp.Id,
                        Date = targetDate,
                        IsPresent = isPresent,
                        CheckInTime = isPresent ? DateTime.Now : null,
                        CreatedAt = DateTime.Now
                    };
                    await _context.EmployeeAttendances.AddAsync(att);
                }
                else
                {
                    existing.IsPresent = isPresent;
                    existing.CheckInTime = isPresent ? (existing.CheckInTime ?? DateTime.Now) : null;
                    _context.EmployeeAttendances.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAttendanceAsync(int id)
        {
            var record = await _context.EmployeeAttendances.FindAsync(id);
            if (record != null)
            {
                _context.EmployeeAttendances.Remove(record);
                await _context.SaveChangesAsync();
            }
        }
    }
}
