using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;

namespace Test_DATA.Controllers
{
    public class ExpensesController : Controller
    {
        private readonly IExpenseService _expenseService;
        private readonly ILookupService _lookupService;
        private readonly IInventoryService _inventoryService;
        private readonly IEmployeeService _employeeService;

        public ExpensesController(
            IExpenseService expenseService,
            ILookupService lookupService,
            IInventoryService inventoryService,
            IEmployeeService employeeService)
        {
            _expenseService = expenseService;
            _lookupService = lookupService;
            _inventoryService = inventoryService;
            _employeeService = employeeService;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, int? categoryId)
        {
            var categories = await _lookupService.GetExpenseCategoriesAsync();
            ViewBag.Categories = await _lookupService.GetExpenseCategoriesAsync();
            ViewBag.RawMaterials = await _inventoryService.GetAllRawMaterialsAsync();
            //ViewBag.Employees = await _employeeService.GetAllEmployeesAsync(true);
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.SelectedCategoryId = categoryId;

            var operationalCategory = categories.FirstOrDefault(c => c.Name.Contains("تشغيل") || c.Name.Contains("عامة"))
                              ?? categories.FirstOrDefault();
            ViewBag.OperationalCategoryId = operationalCategory?.Id ?? 0;
            var expenses = await _expenseService.GetAllExpensesAsync(startDate, endDate, categoryId);
            return View(expenses);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Expense expense)//, decimal? TotalAmountOperating)//, int? linkedRawMaterialId)
        {
            try
            {
               

                if (string.IsNullOrWhiteSpace(expense.Name))
                {
                    TempData["ErrorMessage"] = "يرجى تقديم اسم المصروف.";
                    return RedirectToAction(nameof(Index));
                }
                if (expense.TotalAmount <= 0 && (expense.Quantity * expense.UnitPrice) <= 0)
                {
                    TempData["ErrorMessage"] = "يرجى إدخال مبلغ صحيح للمصروف.";
                    return RedirectToAction(nameof(Index));
                }

                await _expenseService.AddExpenseAsync(expense);//, linkedRawMaterialId);
                TempData["SuccessMessage"] = "تم إضافة المصروف بنجاح وترحيله للخزينة!";
            }
            catch (Exception ex)
            {
               //return Content(ex.ToString());
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Expense expense)
        {
            try
            {
                await _expenseService.UpdateExpenseAsync(expense);
                TempData["SuccessMessage"] = "تم تعديل المصروف وتحديث البيانات بنجاح!";
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
                await _expenseService.DeleteExpenseAsync(id);
                TempData["SuccessMessage"] = "تم حذف المصروف بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> PayRemaining(int expenseId, decimal amountPaidNow, PaymentMethod paymentMethod)
        {
            try
            {
                await _expenseService.PayRemainingAsync(expenseId, amountPaidNow, paymentMethod);
                TempData["SuccessMessage"] = "تم تسجيل سداد المبلغ المتبقي بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
