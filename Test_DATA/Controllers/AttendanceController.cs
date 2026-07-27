using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bakery.Business.Services;

namespace Test_DATA.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;

        public AttendanceController(IAttendanceService attendanceService, IEmployeeService employeeService)
        {
            _attendanceService = attendanceService;
            _employeeService = employeeService;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = targetDate.ToString("yyyy-MM-dd");

            var attendances = await _attendanceService.GetDailyAttendanceAsync(targetDate);
            return View(attendances);
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(int employeeId, DateTime date, bool isPresent, string? notes)
        {
            try
            {
                await _attendanceService.ConfirmAttendanceAsync(employeeId, date, isPresent, notes);
                var statusStr = isPresent ? "حاضر 🟢" : "غائب 🔴";
                TempData["SuccessMessage"] = $"تم تأكيد حالة الحضور ({statusStr}) بنجاح!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        public async Task<IActionResult> SaveBulk(DateTime date, List<int> presentEmployeeIds)
        {
            try
            {
                await _attendanceService.SaveBulkAttendanceAsync(date, presentEmployeeIds);
                TempData["SuccessMessage"] = "تم حفظ كشف الحضور والانصراف اليومي لجميع العاملين بنجاح! 📋✨";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
        }
    }
}
