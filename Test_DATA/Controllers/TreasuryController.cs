using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;
using Bakery.Domain.Enums;

namespace Test_DATA.Controllers
{
    public class TreasuryController : Controller
    {
        private readonly ITreasuryService _treasuryService;

        public TreasuryController(ITreasuryService treasuryService)
        {
            _treasuryService = treasuryService;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, TreasuryTransactionType? type)
        {
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.SelectedType = type;

            ViewBag.Summary = await _treasuryService.GetTreasurySummaryAsync(startDate, endDate);
            var transactions = await _treasuryService.GetTransactionsAsync(startDate, endDate, type);

            return View(transactions);
        }
    }
}
