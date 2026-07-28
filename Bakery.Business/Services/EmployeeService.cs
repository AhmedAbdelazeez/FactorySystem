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

namespace Bakery.Business.Services
{
    public interface IEmployeeService
    {
        Task<IEnumerable<Employee>> GetAllEmployeesAsync(bool? activeOnly = null);
        Task<Employee?> GetEmployeeByIdAsync(int id);
        Task AddEmployeeAsync(Employee employee);
        Task UpdateEmployeeAsync(Employee employee);
        Task DeleteEmployeeAsync(int id);
        Task PaySalaryAsync(int employeeId, decimal salaryAmount, PaymentMethod paymentMethod, string? notes = null);
    }

    public class EmployeeService : IEmployeeService
    {
        private readonly BakeryDbContext _context;
        private readonly IRepository<Employee> _empRepo;
        private readonly IExpenseService _expenseService;

        public EmployeeService(BakeryDbContext context, IRepository<Employee> empRepo, IExpenseService expenseService)
        {
            _context = context;
            _empRepo = empRepo;
            _expenseService = expenseService;
        }

        public async Task<IEnumerable<Employee>> GetAllEmployeesAsync(bool? activeOnly = null)
        {
            var query = _context.Employees.AsQueryable();

            if (activeOnly.HasValue && activeOnly.Value)
            {
                query = query.Where(e => e.IsActive);
            }

            return await query.OrderBy(e => e.Name).ToListAsync();
        }

        public async Task<Employee?> GetEmployeeByIdAsync(int id)
        {
            return await _empRepo.GetByIdAsync(id);
        }

        public async Task AddEmployeeAsync(Employee employee)
        {
            await _empRepo.AddAsync(employee);
            await _empRepo.SaveChangesAsync();
        }

        public async Task UpdateEmployeeAsync(Employee employee)
        {
            var existing = await _empRepo.GetByIdAsync(employee.Id);
            if (existing == null) throw new KeyNotFoundException("العامل غير موجود.");

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

        public async Task PaySalaryAsync(int employeeId, decimal salaryAmount, PaymentMethod paymentMethod, string? notes = null)
        {
            var emp = await _empRepo.GetByIdAsync(employeeId);
            if (emp == null) throw new KeyNotFoundException("العامل غير موجود.");

            var salaryPaid = await _context.Expenses
                .AnyAsync(e => e.EmployeeId == employeeId
                       && e.Date.Year == DateTime.Now.Year
                       && e.Date.Month == DateTime.Now.Month);
                       

            if(salaryPaid) 
                throw new InvalidOperationException("تم صرف الراتب لهذا الشهر بالفعل.");

            var laborCategory = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == "عمالة");
            int categoryId = laborCategory?.Id ?? 1;

            var expense = new Expense
            {
                Name = $"راتب العامل: {emp.Name}",
                ExpenseCategoryId = categoryId,
                Quantity = 1,
                UnitPrice = salaryAmount,
                TotalAmount = salaryAmount,
                Date = DateTime.Now,
                PaymentMethod = paymentMethod,
                PaidAmount = salaryAmount,
                RemainingAmount = 0,
                EmployeeId = emp.Id,
                Notes = notes ?? $"صرف راتب شهر {DateTime.Now:yyyy/MM}"
            };

            await _expenseService.AddExpenseAsync(expense);
        }
    }
}
