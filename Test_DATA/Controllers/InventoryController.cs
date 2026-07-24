using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Domain.Entities;

namespace Test_DATA.Controllers
{
    public class InventoryController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly ILookupService _lookupService;

        public InventoryController(IInventoryService inventoryService, ILookupService lookupService)
        {
            _inventoryService = inventoryService;
            _lookupService = lookupService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.MeasurementUnits = await _lookupService.GetMeasurementUnitsAsync();
            ViewBag.TotalInventoryValue = await _inventoryService.GetTotalInventoryValueAsync();

            var materials = await _inventoryService.GetAllRawMaterialsAsync();
            return View(materials);
        }

        [HttpPost]
        public async Task<IActionResult> Create(RawMaterial material)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(material.Name))
                {
                    TempData["ErrorMessage"] = "يرجى إدخال اسم المادة الخام.";
                    return RedirectToAction(nameof(Index));
                }

                await _inventoryService.AddRawMaterialAsync(material);
                TempData["SuccessMessage"] = "تم إضافة المادة الخام للمخزن بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(RawMaterial material)
        {
            try
            {
                await _inventoryService.UpdateRawMaterialAsync(material);
                TempData["SuccessMessage"] = "تم تعديل المادة الخام بنجاح!";
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
                await _inventoryService.DeleteRawMaterialAsync(id);
                TempData["SuccessMessage"] = "تم حذف المادة الخام بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddStock(int rawMaterialId, decimal quantity, decimal unitPrice, string? notes)
        {
            try
            {
                await _inventoryService.AddStockAsync(rawMaterialId, quantity, unitPrice, notes);
                TempData["SuccessMessage"] = "تم إضافة الكمية الموردة إلى المخزن بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Transactions(int? rawMaterialId)
        {
            ViewBag.RawMaterials = await _inventoryService.GetAllRawMaterialsAsync();
            ViewBag.SelectedMaterialId = rawMaterialId;

            var transactions = await _inventoryService.GetTransactionsAsync(rawMaterialId);
            return View(transactions);
        }
    }
}
