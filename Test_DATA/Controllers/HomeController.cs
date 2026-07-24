using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;

namespace Test_DATA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITreasuryService _treasuryService;

        public HomeController(ITreasuryService treasuryService)
        {
            _treasuryService = treasuryService;
        }

        public async Task<IActionResult> Index()
        {
            var summary = await _treasuryService.GetDashboardSummaryAsync();
            return View(summary);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
