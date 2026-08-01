using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;

namespace Test_DATA.Controllers
{
    public class EmployeesController : Controller
    {
        private readonly IEmployeeService _employeeService;
        private readonly ILookupService _lookupService;

        public EmployeesController(IEmployeeService employeeService, ILookupService lookupService)
        {
            _employeeService = employeeService;
            _lookupService = lookupService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Weekdays = _lookupService.GetWeekdays();
            ViewBag.JobTitles = _lookupService.GetDefaultJobTitles();

            var employees = await _employeeService.GetAllEmployeesAsync();
            return View(employees);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var employeeDetails = await _employeeService.GetEmployeeByIdAsync(id);
                if (employeeDetails == null)
                {
                    TempData["ErrorMessage"] = "العامل غير موجود.";
                    return RedirectToAction(nameof(Index));
                }
                return View(employeeDetails);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
        [HttpPost]
        public async Task<IActionResult> Create(Employee employee)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(employee.Name))
                {
                    TempData["ErrorMessage"] = "يرجى كتابة اسم العامل.";
                    return RedirectToAction(nameof(Index));
                }

                await _employeeService.AddEmployeeAsync(employee);
                TempData["SuccessMessage"] = "تم إضافة العامل بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Employee employee)
        {
            try
            {
                await _employeeService.UpdateEmployeeAsync(employee);
                TempData["SuccessMessage"] = "تم تعديل بيانات العامل بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _employeeService.DeleteEmployeeAsync(id);
                TempData["SuccessMessage"] = "تم حذف العامل بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> PaySalary(int employeeId, PaymentMethod paymentMethod, string? notes, DateTime? targetMonth = null)
        {
            try
            {
                await _employeeService.PaySalaryAsync(employeeId, paymentMethod, notes,targetMonth);
                TempData["SuccessMessage"] = $"تم صرف الراتب بنجاح ✅ | {notes}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddAdvance(int employeeId, decimal advanceAmount, PaymentMethod paymentMethod, string? notes)
        {
            try
            {
                await _employeeService.AddAdvanceAsync(employeeId, advanceAmount, paymentMethod, notes);
                TempData["SuccessMessage"] = "تم صرف السلفة بنجاح وخصمها من الخزينة، وستُخصم من راتب هذا الشهر.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
