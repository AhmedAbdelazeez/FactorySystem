using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Domain.Entities;

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
            await _lookupService.SyncRawMaterialsAsync();
            ViewBag.Categories = await _lookupService.GetExpenseCategoriesAsync();
            ViewBag.Units = await _lookupService.GetMeasurementUnitsAsync();
            ViewBag.MaterialTypes = await _lookupService.GetMaterialTypesAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddMaterialType(string name, int measurementUnitId)
        {
            try
            {
                await _lookupService.AddMaterialTypeAsync(name, measurementUnitId);
                TempData["SuccessMessage"] = "تم إضافة نوع المادة الخام بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // --- التعديل هنا: استقبال وحدات القياس مع الاسم والمعرف ---
        [HttpPost]
        public async Task<IActionResult> EditMaterialType(int id, string name, int measurementUnitId)
        {
            try
            {
                await _lookupService.UpdateMaterialAsync(id, name, measurementUnitId);
                TempData["SuccessMessage"] = "تم تعديل نوع المادة الخام بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMaterialType(int id)
        {
            try
            {
                await _lookupService.DeleteMaterialAsync(id);
                TempData["SuccessMessage"] = "تم حذف نوع المادة الخام بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
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

        [HttpPost]
        public async Task<IActionResult> EditUnit(MeasurementUnit unit)
        {
            try
            {
                await _lookupService.UpdateMeasurementUnitAsync(unit);
                TempData["SuccessMessage"] = "تم تعديل وحدة القياس بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUnit(int id)
        {
            try
            {
                await _lookupService.DeleteMeasurementUnitAsync(id);
                TempData["SuccessMessage"] = "تم حذف وحدة القياس بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}