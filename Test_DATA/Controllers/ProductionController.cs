using Bakery.Business.DTOs;
using Bakery.Business.Services;
using Bakery.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Test_DATA.Controllers
{
    public class ProductionController : Controller
    {
        private readonly IProductionService _productionService;
        private readonly IAgentService _agentService;

        public ProductionController(IProductionService productionService, IAgentService agentService)
        {
            _productionService = productionService;
            _agentService = agentService;
        }

        // ─────────────────────────────────────────────────────────
        // Production Orders
        // ─────────────────────────────────────────────────────────

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            ViewBag.Settings = await _productionService.GetProductionSettingsAsync();
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            var orders = await _productionService.GetAllProductionOrdersAsync(startDate, endDate);
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> CalculateApi(decimal flourSackCount, ProductType productType, decimal? customBasketPrice)
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
                await _productionService.CreateProductionOrderAsync(flourSackCount, productType, notes, customBasketPrice);
                TempData["SuccessMessage"] = $"تم إنشاء أمر إنتاج لعدد ({flourSackCount}) شكارة دقيق بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateActual(int id, decimal actualQuantity, string? notes)
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

        // ─────────────────────────────────────────────────────────
        // Sales + Agents
        // ─────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Sales(DateTime? filterDate)
        {
            try
            {
                var agents = await _agentService.GetAllAgentsAsync();
                ViewBag.Agents = agents;
                ViewBag.AvailableOrders = await _productionService.GetAvailableOrdersForSaleAsync();
                ViewBag.FilterDate = filterDate?.ToString("yyyy-MM-dd");
                var salesHistory = await _productionService.GetSalesHistoryAsync(filterDate);

                ViewBag.MabroumStock = await _productionService.GetProductTypeStockAsync(ProductType.Mabroum);
                ViewBag.PaneStock = await _productionService.GetProductTypeStockAsync(ProductType.Pane);
                ViewBag.SandwichStock = await _productionService.GetProductTypeStockAsync(ProductType.Sandwich);

                return View(salesHistory);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء تحميل بيانات المبيعات: {ex.Message}";
                ViewBag.Agents = new List<AgentDto>();
                ViewBag.AvailableOrders = new List<ProductionOrderDto>();
                return View(new List<ProductOrderSalesHistoryDto>());
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

        // ─────────────────────────────────────────────────────────
        // Agent CRUD
        // ─────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> CreateAgent(CreateAgentDto dto)
        {
            try
            {
                await _agentService.CreateAgentAsync(dto);
                TempData["SuccessMessage"] = $"تم إضافة الوكيل '{dto.Name}' بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Sales));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAgent(CreateAgentDto dto)
        {
            try
            {
                await _agentService.UpdateAgentAsync(dto);
                TempData["SuccessMessage"] = "تم تحديث بيانات الوكيل بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Sales));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAgent(int id)
        {
            try
            {
                await _agentService.DeleteAgentAsync(id);
                TempData["SuccessMessage"] = "تم حذف/تعطيل الوكيل بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Sales));
        }

        [HttpGet]
        public async Task<IActionResult> AgentSummaryApi(int agentId)
        {
            try
            {
                var summary = await _agentService.GetAgentWithSummaryAsync(agentId);
                return Json(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}