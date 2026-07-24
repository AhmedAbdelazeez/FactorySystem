using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;

namespace Test_DATA.Controllers
{
    public class LookupsController : Controller
    {
        private readonly ILookupService _lookupService;

        public LookupsController(ILookupService lookupService)
        {
            _lookupService = lookupService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _lookupService.GetExpenseCategoriesAsync();
            ViewBag.Units = await _lookupService.GetMeasurementUnitsAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddCategory(string name)
        {
            try
            {
                await _lookupService.AddExpenseCategoryAsync(name);
                TempData["SuccessMessage"] = "تم إضافة تصنيف المصروفات بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddUnit(string name)
        {
            try
            {
                await _lookupService.AddMeasurementUnitAsync(name);
                TempData["SuccessMessage"] = "تم إضافة وحدة القياس بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
