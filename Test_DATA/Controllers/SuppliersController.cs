using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Business.DTOs;
using Bakery.Domain.Enums;

namespace Test_DATA.Controllers
{
    public class SuppliersController : Controller
    {
        private readonly ISupplierService _supplierService;
        private readonly IInventoryService _inventoryService;

        public SuppliersController(ISupplierService supplierService, IInventoryService inventoryService)
        {
            _supplierService = supplierService;
            _inventoryService = inventoryService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.FinancialSummary = await _supplierService.GetFinancialSummaryAsync();
            ViewBag.AllInvoices = await _supplierService.GetAllInvoicesAsync();
            ViewBag.RawMaterials = await _inventoryService.GetAllRawMaterialsAsync();

            var suppliers = await _supplierService.GetAllSuppliersSummaryAsync();
            return View(suppliers);
        }

        public async Task<IActionResult> Details(int id)
        {
            var supplier = await _supplierService.GetSupplierDetailsByIdAsync(id);
            if (supplier == null)
            {
                TempData["ErrorMessage"] = "المورد غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RawMaterials = await _inventoryService.GetAllRawMaterialsAsync();
            ViewBag.Summary = await _supplierService.GetSupplierSummaryByIdAsync(id);
            return View(supplier);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSupplier([FromForm] CreateSupplierDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    TempData["ErrorMessage"] = "يرجى إدخال اسم المورد.";
                    return RedirectToAction(nameof(Index));
                }

                await _supplierService.AddSupplierAsync(dto);
                TempData["SuccessMessage"] = $"تم إضافة المورد ({dto.Name}) وتعيين قائمة الأصناف بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> EditSupplier([FromForm] CreateSupplierDto dto)
        {
            try
            {
                await _supplierService.UpdateSupplierAsync(dto);
                TempData["SuccessMessage"] = "تم تعديل بيانات المورد وقائمة الأصناف بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            try
            {
                await _supplierService.DeleteSupplierAsync(id);
                TempData["SuccessMessage"] = "تم حذف المورد بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CreateInvoice([FromForm] CreateSupplierInvoiceDto dto, int? returnSupplierId)
        {
            try
            {
                var invoice = await _supplierService.AddInvoiceAsync(dto);
                TempData["SuccessMessage"] = $"تم إضافة الفاتورة بنجاح رقم #{invoice.InvoiceNumber} وتم انعكاس الكمية والأسعار بالمخزن فوراً!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            if (returnSupplierId.HasValue && returnSupplierId.Value > 0)
            {
                return RedirectToAction(nameof(Details), new { id = returnSupplierId.Value });
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> PayInvoice(int invoiceId, decimal amountPaidNow, PaymentMethod paymentMethod, int? returnSupplierId)
        {
            try
            {
                await _supplierService.PayInvoiceRemainingAsync(invoiceId, amountPaidNow, paymentMethod);
                TempData["SuccessMessage"] = $"تم تسديد مبلغ {amountPaidNow:N2} ج.م من المديونية بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            if (returnSupplierId.HasValue && returnSupplierId.Value > 0)
            {
                return RedirectToAction(nameof(Details), new { id = returnSupplierId.Value });
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
