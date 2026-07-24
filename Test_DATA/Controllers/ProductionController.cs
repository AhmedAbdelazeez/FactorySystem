using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;

namespace Test_DATA.Controllers
{
    public class ProductionController : Controller
    {
        private readonly IProductionService _productionService;

        public ProductionController(IProductionService productionService)
        {
            _productionService = productionService;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            ViewBag.Settings = await _productionService.GetProductionSettingsAsync();
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            var orders = await _productionService.GetAllProductionOrdersAsync(startDate, endDate);
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> CalculateApi(decimal flourSackCount, decimal? customBasketPrice)
        {
            try
            {
                var calc = await _productionService.CalculateProductionTargetAsync(flourSackCount, customBasketPrice);
                return Json(new { success = true, data = calc });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(decimal flourSackCount, string? notes, decimal? customBasketPrice)
        {
            try
            {
                var order = await _productionService.CreateProductionOrderAsync(flourSackCount, notes, customBasketPrice);
                TempData["SuccessMessage"] = $"تم إنشاء أمر إنتاج لعدد ({flourSackCount}) شكارة دقيق بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateActual(int id, int actualMabroum, int actualPane, int actualSandwich, string? notes)
        {
            try
            {
                await _productionService.UpdateActualProductionAsync(id, actualMabroum, actualPane, actualSandwich, notes);
                TempData["SuccessMessage"] = "تم تسجيل ونقل نتائج الإنتاج الفعلي بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(int id)
        {
            try
            {
                await _productionService.ConfirmProductionOrderAsync(id);
                TempData["SuccessMessage"] = "تم تأكيد عملية الإنتاج وخصم الخامات من المخزن وتسجيل المبيعات في الخزينة بنجاح! 🥖✨";
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
                await _productionService.DeleteProductionOrderAsync(id);
                TempData["SuccessMessage"] = "تم حذف أمر الإنتاج بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
