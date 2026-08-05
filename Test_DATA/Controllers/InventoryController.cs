using Bakery.Business.Services;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

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
            ViewBag.MaterialTypes = await _lookupService.GetMaterialTypesAsync();
            ViewBag.TotalInventoryValue = await _inventoryService.GetTotalInventoryValueAsync();

            var materials = await _inventoryService.GetAllRawMaterialsAsync();
            return View(materials);
        }

        [HttpPost]
        public async Task<IActionResult> Create(RawMaterial material , PaymentMethod paymentMethod,decimal paidAmount=0,string? notes=null)
        {
            try
            {
                //if (string.IsNullOrWhiteSpace(material.MaterialType?.Name))
                //{
                //    TempData["ErrorMessage"] = "يرجى إدخال اسم المادة الخام.";
                //    return RedirectToAction(nameof(Index));
                //}

                await _inventoryService.AddRawMaterialAsync(material, paymentMethod, paidAmount, notes);
                TempData["SuccessMessage"] = "تم إضافة المادة الخام للمخزن بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        //[HttpPost]
        //public async Task<IActionResult> Edit(RawMaterial material)
        //{
        //    try
        //    {
        //        await _inventoryService.UpdateRawMaterialAsync(material);
        //        TempData["SuccessMessage"] = "تم تعديل المادة الخام بنجاح!";
        //    }
        //    catch (Exception ex)
        //    {
        //        TempData["ErrorMessage"] = ex.Message;
        //    }
        //    return RedirectToAction(nameof(Index));
        //}

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
        public async Task<IActionResult> AddStock(int rawMaterialId, decimal quantity, decimal unitPrice,PaymentMethod paymentMethod, decimal paidAmount = 0, string? notes=null)
        {
            try
            {
                await _inventoryService.AddStockAsync(rawMaterialId, quantity, unitPrice, paymentMethod, paidAmount, notes);
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


        [HttpPost]
        public async Task<IActionResult> UpdateTransaction(int transactionId, decimal newQuantity, decimal newUnitPrice, PaymentMethod paymentMethod, decimal paidAmount, string? notes = null)
        {
            try
            {
                await _inventoryService.UpdateTransactionAsync( transactionId, newQuantity, newUnitPrice,paymentMethod,paidAmount, notes);
                TempData["SuccessMessage"] = "تم تعديل المعاملة بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Transactions));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTransaction(int transactionId)
        {
            try
            {
                await _inventoryService.DeleteTransactionAsync(transactionId);
                TempData["SuccessMessage"] = "تم حذف المعاملة بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Transactions));
        }

    }
}
