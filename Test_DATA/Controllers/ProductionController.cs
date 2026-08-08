using Bakery.Business.DTOs;
using Bakery.Business.Services;
using Bakery.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

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
        public async Task<IActionResult> CalculateApi(decimal flourSackCount, ProductType productType,decimal? customBasketPrice)
        {
            try
            {
                var calc = await _productionService.CalculateProductionTargetAsync(flourSackCount, productType, customBasketPrice);
                return Json(new { success = true, data = calc });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(decimal flourSackCount, ProductType productType, string? notes, decimal? customBasketPrice)
        {
            try
            {
                var order = await _productionService.CreateProductionOrderAsync(flourSackCount, productType, notes, customBasketPrice);
                TempData["SuccessMessage"] = $"تم إنشاء أمر إنتاج لعدد ({flourSackCount}) شكارة دقيق بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateActual(int id, int actualQuantity, string? notes)
        {
            try
            {
                await _productionService.UpdateActualProductionAsync(id, actualQuantity, notes);
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

                TempData["SuccessMessage"] = "تم تأكيد عملية الإنتاج وخصم الخامات من المخزن بنجاح! 🥖✨";
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

        // sale recording

        [HttpGet]
        public async Task<IActionResult> Sales()
        {
            try
            {
                ViewBag.AvailableOrders = await _productionService.GetAvailableOrdersForSaleAsync();
                var salesHistory = await _productionService.GetSalesHistoryAsync();
                return View(salesHistory);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء تحميل بيانات المبيعات: {ex.Message}";
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> RecordSale(CreateProductSaleDto dto)
        {
            try
            {
                await _productionService.RecordProductSaleAsync(dto);
                TempData["SuccessMessage"] = $"تم تسجيل بيع عدد ({dto.SoldBaskets}) باسكت بنجاح وتسجيل الحركة المالية بالخزينة!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Sales));
        }

        [HttpPost]
        public async Task<IActionResult> CollectRemaining(int transactionId, decimal amountToCollect, PaymentMethod paymentMethod, string? notes)
        {
            try
            {
                await _productionService.CollectRemainingSaleAmountAsync(transactionId, amountToCollect, paymentMethod, notes);
                TempData["SuccessMessage"] = "تم تسجيل تحصيل المبلغ المتبقي وتحديث الخزينة بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Sales));
        }

        [HttpPost]
        public async Task<IActionResult> CancelSale(int id)
        {
            try
            {
                await _productionService.CancelProductSaleAsync(id);
                TempData["SuccessMessage"] = "تم إلغاء عملية البيع وإعادة الباسكيت لرصيد أمر الإنتاج بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Sales));
        }
    }
}
    

