using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Domain.Entities;

namespace Test_DATA.Controllers
{
    public class SettingsController : Controller
    {
        private readonly IProductionService _productionService;

        public SettingsController(IProductionService productionService)
        {
            _productionService = productionService;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _productionService.GetProductionSettingsAsync();
            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Update(ProductionSetting settings)
        {
            try
            {
                await _productionService.UpdateProductionSettingsAsync(settings);
                TempData["SuccessMessage"] = "تم تحديث إعدادات أسعار ومعدلات الباسكيت للإنتاج بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
