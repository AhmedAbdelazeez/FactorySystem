using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Domain.Entities;

namespace Test_DATA.Controllers
{
    public class RecipeController : Controller
    {
        private readonly IProductionService _productionService;
        private readonly IInventoryService _inventoryService;

        public RecipeController(IProductionService productionService, IInventoryService inventoryService)
        {
            _productionService = productionService;
            _inventoryService = inventoryService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.RawMaterials = await _inventoryService.GetAllRawMaterialsAsync();
            var recipe = await _productionService.GetActiveRecipeAsync();
            return View(recipe);
        }

        [HttpPost]
        public async Task<IActionResult> Save(List<ProductionRecipeItem> items)
        {
            try
            {
                await _productionService.SaveRecipeItemsAsync(items);
                TempData["SuccessMessage"] = "تم حفظ مكونات وصفة شكارة الدقيق بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
