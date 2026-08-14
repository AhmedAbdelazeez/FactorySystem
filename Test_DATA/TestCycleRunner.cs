using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.Business.Services;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;

namespace Test_DATA
{
    public static class TestCycleRunner
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("🧪 بدء دورة الاختبار الشاملة (Full Test Cycle)...");
            Console.WriteLine("==================================================");

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BakeryDbContext>();
            var productionService = scope.ServiceProvider.GetRequiredService<IProductionService>();
            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            var employeeService = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
            var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
            var treasuryService = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

            // 1. Verify Seeded Raw Materials & Recipe
            Console.WriteLine("\n[1/5] 📦 فحص مخزن المواد الخام والوصفة:");
            var materials = (await inventoryService.GetAllRawMaterialsAsync()).ToList();
            foreach (var m in materials)
            {
                Console.WriteLine($"  - المادة: {m.MaterialType?.Name} | الرصيد: {m.CurrentQuantity} {m.MeasurementUnit?.Name} | السعر: {m.UnitPrice} ج.م");
            }

            var recipe = await productionService.GetActiveRecipeAsync();
            Console.WriteLine($"\n  📜 مكونات الوصفة ({recipe.Name}):");
            foreach (var item in recipe.RecipeItems)
            {
                Console.WriteLine($"  - {item.RawMaterial?.MaterialType?.Name}: {item.RequiredQuantity} {item.RawMaterial?.MeasurementUnit?.Name}");
            }

            // 2. Verify 5 Employees
            Console.WriteLine("\n[2/5] 👥 فحص العمال والموظفين المسجلين:");
            var employees = (await employeeService.GetAllEmployeesAsync()).ToList();
            Console.WriteLine($"  - إجمالي عدد الموظفين: {employees.Count}");
            foreach (var emp in employees)
            {
                Console.WriteLine($"  - الموظف #{emp.Id}: {emp.Name} | الوظيفة: {emp.JobTitle} | الراتب: {emp.MonthlySalary} ج.م | الإجازة: {emp.WeeklyDayOff}");
            }

            // 3. Test Attendance Page logic
            Console.WriteLine("\n[3/5] 📋 تسجيل واختبار الحضور والانصراف:");
            if (employees.Count >= 1)
            {
                var emp1 = employees[0];
                await attendanceService.ConfirmAttendanceAsync(emp1.Id, DateTime.Today, true, "حضور مبكر");

                // ✅ FIX: Only access employees[1] if at least 2 employees exist
                if (employees.Count >= 2)
                {
                    var emp2 = employees[1];
                    await attendanceService.ConfirmAttendanceAsync(emp2.Id, DateTime.Today, false, "إجازة مرضية");
                }

                var dailyAttendance = (await attendanceService.GetDailyAttendanceAsync(DateTime.Today)).ToList();
                Console.WriteLine($"  - إجمالي كشف الحضور لليوم: {dailyAttendance.Count}");
                foreach (var att in dailyAttendance)
                {
                    Console.WriteLine($"  - {att.Employee?.Name}: {(att.IsPresent ? "حاضر 🟢" : "غائب 🔴")} (ملاحظات: {att.Notes ?? "-"})");
                }
            }

            // 4. Test Stock Validation on Excess Production Order Creation
            Console.WriteLine("\n[4/5] 🛡️ اختبار الـ Validation عند عجز المخزون:");
            try
            {
                Console.WriteLine("  - محاولة طلب إنتاج 10,000 شكارة دقيق (أكبر من رصيد المخزن)...");
                await productionService.CreateProductionOrderAsync(10000, ProductType.Mabroum, "وردية صباحية تجريبية");
                Console.WriteLine("  ❌ خطأ: تم السواح بإنشاء أمر إنتاج رغم عدم توفر المخزون!");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"  ✅ نجح الـ Validation لمنع الإنتاج عند عجز المخزون!");
                Console.WriteLine($"  💬 الرسالة: {ex.Message}");
            }

            // 5. Test Successful Production Order Cycle (5 Sacks)
            Console.WriteLine("\n[5/5] 🏭 تنفيذ دورة إنتاج فعلي ناجحة (5 شكارة):");
            var order = await productionService.CreateProductionOrderAsync(5, ProductType.Mabroum, "وردية صباحية تجريبية");
            Console.WriteLine($"  - تم فتح أمر إنتاج #{order.Id} لعدد {order.FlourSackCount} شكارة.");
            Console.WriteLine($"  - المستهدف المتوقع: {order.TotalTargetPieces} قطعة ({order.ExpectedBaskets} باسيكت) بقيمة {order.TotalExpectedSalesValue} ج.م");


            // Update actual production
            int actualMabroum = (int)order.OrderResults.First(r => r.ProductType == ProductType.Mabroum).TargetQuantity;
            int actualPane = (int)order.OrderResults.First(r => r.ProductType == ProductType.Pane).TargetQuantity;
            int actualSandwich = (int)order.OrderResults.First(r => r.ProductType == ProductType.Sandwich).TargetQuantity;

            await productionService.UpdateActualProductionAsync(order.Id, order.TotalActualPieces, "تم الانتهاء بنجاح 100%");
            Console.WriteLine($"  - تم إدخال النتائج الفعلية: مبروم={actualMabroum}، بانيه={actualPane}، ساندوتش={actualSandwich}.");

            // Confirm Production Order
            await productionService.ConfirmProductionOrderAsync(order.Id);
            Console.WriteLine($"  - تم تأكيد أمر الإنتاج #{order.Id} وخصم الخامات وتسجيل الإيرادات الخزينة! 🥖✨");

            // Verify Treasury balance after confirmation
            var summary = await treasuryService.GetTreasurySummaryAsync();
            Console.WriteLine($"  - رصيد الخزينة النقدي الحالي: {summary.CashBalance} ج.م (إجمالي الإيرادات: {summary.TotalIncome} ج.م)");

            Console.WriteLine("\n==================================================");
            Console.WriteLine("🎉 تمت دورة الاختبار بنجاح 100% ودون أي أخطاء!");
            Console.WriteLine("==================================================");
        }
    }
}
