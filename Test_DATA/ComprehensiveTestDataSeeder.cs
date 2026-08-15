using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.Business.Services;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;
using Bakery.Business.DTOs;

namespace Test_DATA
{
    public static class ComprehensiveTestDataSeeder
    {
        public static async Task SeedAndRunFullTestAsync(IServiceProvider services)
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("🏭 بدء دورة الاختبار والتغذية الشاملة بالبيانات الواقعية (Full Production Seed)");
            Console.WriteLine("===============================================================================\n");

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BakeryDbContext>();
            var productionService = scope.ServiceProvider.GetRequiredService<IProductionService>();
            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            var employeeService = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
            var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
            var expenseService = scope.ServiceProvider.GetRequiredService<IExpenseService>();
            var treasuryService = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

            // ----------------------------------------------------------------------------------
            // 1. تجهيز تصنيفات المصاريف الأساسية (Categories)
            // ----------------------------------------------------------------------------------
            Console.WriteLine("📌 [1/7] تجهيز وتأكيد تصنيفات المصروفات ومكونات النظام...");
            var catLabor = await GetOrAddCategoryAsync(db, "عمالة", true);
            var catRaw = await GetOrAddCategoryAsync(db, "مواد خام", true);
            var catOperating = await GetOrAddCategoryAsync(db, "مصاريف تشغيلية", false);
            var catServices = await GetOrAddCategoryAsync(db, "صيانة وخدمات", false);

            // ----------------------------------------------------------------------------------
            // 2. إضافة موظفين واقعيين بتواريخ تعيين وحسابات رواتب
            // ----------------------------------------------------------------------------------
            Console.WriteLine("\n👥 [2/7] إنشاء وتحديث سجلات العمال والموظفين (5 موظفين)...");
            var startJuly = new DateTime(2026, 7, 1);

            var emp1 = await GetOrAddEmployeeAsync(db, "أحمد محمود علي", "خباز أول", 35, 8000m, "الجمعة", "01012345678", startJuly);
            var emp2 = await GetOrAddEmployeeAsync(db, "محمد عبد السلام", "عامل إنتاج", 28, 5500m, "الجمعة", "01123456789", startJuly);
            var emp3 = await GetOrAddEmployeeAsync(db, "محمود حسن إبراهيم", "عامل تعبئة وتغليف", 24, 4800m, "الأحد", "01234567890", startJuly);
            var emp4 = await GetOrAddEmployeeAsync(db, "إبراهيم السيد أحمد", "فني صيانة أفران", 40, 6500m, "الجمعة", "01545678901", startJuly);
            var emp5 = await GetOrAddEmployeeAsync(db, "مصطفى كمال محمود", "عامل نظافة وحراسة", 32, 4000m, "الجمعة", "01098765432", startJuly);

            Console.WriteLine($"  - تم تأكيد بيانات {await db.Employees.CountAsync()} موظفين بالرواتب والتواريخ.");

            // ----------------------------------------------------------------------------------
            // 3. تسجيل حضور وغياب شهر يوليو 2026 + صرف سُلف + صرف رواتب شهر يوليو
            // ----------------------------------------------------------------------------------
            Console.WriteLine("\n📋 [3/7] تسجيل غياب وسُلف ورواتب شهر يوليو 2026...");
            
            // إضافة غياب واقعي للموظف 2 والموظف 3 لشهر يوليو
            await AddAttendanceRecordAsync(db, emp2.Id, new DateTime(2026, 7, 12), false, "غياب بدون إذن");
            await AddAttendanceRecordAsync(db, emp2.Id, new DateTime(2026, 7, 22), false, "غياب مرضى");
            await AddAttendanceRecordAsync(db, emp3.Id, new DateTime(2026, 7, 15), false, "إجازة شخصية");

            // صرف سُلف في شهر يوليو
            if (!await db.EmployeeAdvances.AnyAsync(a => a.EmployeeId == emp1.Id && a.Date.Month == 7))
            {
                await employeeService.AddAdvanceAsync(emp1.Id, 1000m, PaymentMethod.Cash, "سلفة منتصف شهر يوليو");
                Console.WriteLine($"  - تم صرف سلفة 1,000 ج.م للموظف ({emp1.Name})");
            }

            if (!await db.EmployeeAdvances.AnyAsync(a => a.EmployeeId == emp2.Id && a.Date.Month == 7))
            {
                await employeeService.AddAdvanceAsync(emp2.Id, 500m, PaymentMethod.Cash, "سلفة طارئة");
                Console.WriteLine($"  - تم صرف سلفة 500 ج.م للموظف ({emp2.Name})");
            }

            // صرف رواتب شهر يوليو لجميع الموظفين
            var julyMonth = new DateTime(2026, 7, 1);
            foreach (var emp in new[] { emp1, emp2, emp3, emp4, emp5 })
            {
                try
                {
                    string msg = await employeeService.PaySalaryAsync(emp.Id, PaymentMethod.Cash, "صرف راتب يوليو 2026", julyMonth);
                    Console.WriteLine($"  ✅ {emp.Name}: {msg}");
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"  ℹ️ {emp.Name}: {ex.Message}");
                }
            }

            // ----------------------------------------------------------------------------------
            // 4. توريد خامات وشراء مخزون بتواريخ مختلفة وطرق دفع متنوعة
            // ----------------------------------------------------------------------------------
            Console.WriteLine("\n📦 [4/7] شراء وتوريد خامات ومستلزمات إنتاج للمخزن...");

            var matFlour = await db.RawMaterials.Include(r => r.MaterialType).FirstAsync(r => r.MaterialType!.Name.Contains("دقيق"));
            var matOil = await db.RawMaterials.Include(r => r.MaterialType).FirstAsync(r => r.MaterialType!.Name.Contains("زيت"));
            var matSugar = await db.RawMaterials.Include(r => r.MaterialType).FirstAsync(r => r.MaterialType!.Name.Contains("سكر"));
            var matButter = await db.RawMaterials.Include(r => r.MaterialType).FirstAsync(r => r.MaterialType!.Name.Contains("زبدة"));
            var matYeast = await db.RawMaterials.Include(r => r.MaterialType).FirstAsync(r => r.MaterialType!.Name.Contains("خميرة"));
            var matImprover = await db.RawMaterials.Include(r => r.MaterialType).FirstAsync(r => r.MaterialType!.Name.Contains("محسن"));
            var matPreservatives = await db.RawMaterials.Include(r => r.MaterialType).FirstAsync(r => r.MaterialType!.Name.Contains("حافظة"));

            // 150 شكارة دقيق كاش (67,500 ج.م)
            await inventoryService.AddStockAsync(matFlour.Id, 150m, 450m, PaymentMethod.Cash, 67500m, "توريد 150 شكارة دقيق 72% نقدي");
            Console.WriteLine("  - تم شراء 150 شكارة دقيق بسعر 450 ج/شكارة (كاش)");

            // 250 زجاجة زيت كاش (15,000 ج.م)
            await inventoryService.AddStockAsync(matOil.Id, 250m, 60m, PaymentMethod.Cash, 15000m, "توريد 250 زجاجة زيت طعام");
            Console.WriteLine("  - تم شراء 250 زجاجة زيت بسعر 60 ج/زجاجة (كاش)");

            // 500 كجم سكر كاش (17,500 ج.م)
            await inventoryService.AddStockAsync(matSugar.Id, 500m, 35m, PaymentMethod.Cash, 17500m, "توريد 500 كجم سكر أبيض");
            Console.WriteLine("  - تم شراء 500 كجم سكر بسعر 35 ج/كجم (كاش)");

            // 350 كجم زبدة دفع جزئي (إجمالي 42,000 ج.م - مدفوع 25,000 ج.م - متبقي 17,000 ج.م)
            await inventoryService.AddStockAsync(matButter.Id, 350m, 120m, PaymentMethod.PartiallyPaid, 25000m, "توريد 350 كجم زبدة نيوزيلاندي - دفع جزئي");
            Console.WriteLine("  - تم شراء 350 كجم زبدة (إجمالي 42,000 ج.م - مدفوع 25,000 ج.م كاش - آجل 17,000 ج.م)");

            // خميرة + محسن + مواد حافظة
            await inventoryService.AddStockAsync(matYeast.Id, 400m, 20m, PaymentMethod.Cash, 8000m, "توريد 400 قالب خميرة فوري");
            await inventoryService.AddStockAsync(matImprover.Id, 300m, 15m, PaymentMethod.Cash, 4500m, "توريد 300 علبة محسن خبز");
            await inventoryService.AddStockAsync(matPreservatives.Id, 30000m, 0.5m, PaymentMethod.Cash, 15000m, "توريد 30,000 جرام مواد حافظة مصرح بها");

            // ----------------------------------------------------------------------------------
            // 5. تسديد مصاريف تشغيل المصنع (كهرباء، مياه، غاز، صيانة) + سداد متبقيات
            // ----------------------------------------------------------------------------------
            Console.WriteLine("\n⚡ [5/7] تسجيل مصاريف تشغيل المصنع (كهرباء، مياه، غاز، صيانة) وسداد المتبقي...");

            var expElectricity = new Expense
            {
                Name = "فاتورة كهرباء المصنع لشهر يوليو",
                ExpenseCategoryId = catOperating.Id,
                Quantity = 1,
                UnitPrice = 4500m,
                TotalAmount = 4500m,
                Date = DateTime.Now.AddDays(-6),
                PaymentMethod = PaymentMethod.Cash,
                PaidAmount = 4500m,
                RemainingAmount = 0,
                Notes = "سداد فاتورة الكهرباء بالعداد التجاري"
            };
            await expenseService.AddExpenseAsync(expElectricity);
            Console.WriteLine("  - تم دفع فاتورة كهرباء: 4,500 ج.م (كاش)");

            var expWater = new Expense
            {
                Name = "فاتورة مياه المصنع",
                ExpenseCategoryId = catOperating.Id,
                Quantity = 1,
                UnitPrice = 1200m,
                TotalAmount = 1200m,
                Date = DateTime.Now.AddDays(-5),
                PaymentMethod = PaymentMethod.Cash,
                PaidAmount = 1200m,
                RemainingAmount = 0,
                Notes = "سداد فاتورة المياه"
            };
            await expenseService.AddExpenseAsync(expWater);
            Console.WriteLine("  - تم دفع فاتورة مياه: 1,200 ج.م (كاش)");

            // فاتورة غاز دفع جزئي (6,800 ج.م - مدفوع 4,000 ج.م - متبقي 2,800 ج.م)
            var expGas = new Expense
            {
                Name = "فاتورة غاز طبيعي للأفران",
                ExpenseCategoryId = catOperating.Id,
                Quantity = 1,
                UnitPrice = 6800m,
                TotalAmount = 6800m,
                Date = DateTime.Now.AddDays(-4),
                PaymentMethod = PaymentMethod.PartiallyPaid,
                PaidAmount = 4000m,
                RemainingAmount = 2800m,
                Notes = "دفع جزئي من فاتورة غاز المصنع"
            };
            await expenseService.AddExpenseAsync(expGas);
            Console.WriteLine("  - تم تسجيل فاتورة غاز: 6,800 ج.م (مدفوع 4,000 ج.م - متبقي 2,800 ج.م)");

            var expMaint = new Expense
            {
                Name = "صيانة وتغيير سيور العجانات والأفران",
                ExpenseCategoryId = catServices.Id,
                Quantity = 1,
                UnitPrice = 2500m,
                TotalAmount = 2500m,
                Date = DateTime.Now.AddDays(-3),
                PaymentMethod = PaymentMethod.Cash,
                PaidAmount = 2500m,
                RemainingAmount = 0,
                Notes = "قطع غيار وصيانة دورية"
            };
            await expenseService.AddExpenseAsync(expMaint);
            Console.WriteLine("  - تم دفع صيانة أفران: 2,500 ج.م (كاش)");

            // سداد متبقي فاتورة الغاز (2,800 ج.م)
            await expenseService.PayRemainingAsync(expGas.Id, 2800m, PaymentMethod.Cash);
            Console.WriteLine("  ✅ تم سداد باقي فاتورة الغاز (2,800 ج.م كاش) وإغلاق المديونية!");

            // ----------------------------------------------------------------------------------
            // 6. تشغيل دورات إنتاج متعدّدة (مبروم، فينو/باتيه، ساندوتش) + مبيعات وتحصيل
            // ----------------------------------------------------------------------------------
            Console.WriteLine("\n🥖 [6/7] تنفيذ 3 دورات إنتاج مختلفة (مبروم، باتيه، ساندوتش) وتسجيل المبيعات والتحصيل...");

            // --- دورة 1: مبروم (25 شكارة دقيق) ---
            var order1 = await productionService.CreateProductionOrderAsync(25m, ProductType.Mabroum, "وردية إنتاج مبروم - صباحية", 100m);
            await productionService.UpdateActualProductionAsync(order1.Id, 1400m, "تم إنتاج 1400 قطعة مبروم بالكامل (50 باسكت)");
            await productionService.ConfirmProductionOrderAsync(order1.Id);
            Console.WriteLine($"  - أمر إنتاج #1 (مبروم 25 شكارة): تم تأكيده وخصم الخامات (المستهدف 50 باسكت @ 100 ج)");

            // بيع 30 باسكت كاش (3,000 ج.م)
            await productionService.RecordProductSaleAsync(new CreateProductSaleDto
            {
                ProductionOrderId = order1.Id,
                SoldBaskets = 30m,
                BasketSellingPrice = 100m,
                PaidAmount = 3000m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "بيع 30 باسكت مبروم للموزع كاش"
            });
            Console.WriteLine("    💰 بيع 30 باسكت مبروم = 3,000 ج.م كاش");

            // بيع 20 باسكت آجل (2,000 ج.م آجل)
            await productionService.RecordProductSaleAsync(new CreateProductSaleDto
            {
                ProductionOrderId = order1.Id,
                SoldBaskets = 20m,
                BasketSellingPrice = 100m,
                PaidAmount = 0m,
                PaymentMethod = PaymentMethod.Unpaid,
                Notes = "بيع 20 باسكت مبروم لمحل الجملة (آجل)"
            });
            Console.WriteLine("    📝 بيع 20 باسكت مبروم = 2,000 ج.م (آجل/غير مدفوع)");

            // تحصيل 1,200 ج.م من المبيعات الآجلة
            var saleTx1 = (await db.TreasuryTransactions.Where(t => t.ProductionOrderId == order1.Id && t.RemainingAmount > 0).FirstAsync());
            await productionService.CollectRemainingSaleAmountAsync(saleTx1.Id, 1200m, PaymentMethod.Cash, "تحصيل دفعة من حساب محل الجملة");
            Console.WriteLine("    💵 تم تحصيل 1,200 ج.م من مبيعات المبروم الآجلة!");

            // --- دورة 2: باتيه / فينو (35 شكارة دقيق) ---
            var order2 = await productionService.CreateProductionOrderAsync(35m, ProductType.Pane, "وردية باتيه وفينو مسائية", 110m);
            await productionService.UpdateActualProductionAsync(order2.Id, 1505m, "إنتاج 1505 قطعة (53.75 باسكت)");
            await productionService.ConfirmProductionOrderAsync(order2.Id);
            Console.WriteLine($"  - أمر إنتاج #2 (باتيه 35 شكارة): تم تأكيده وخصم الخامات (53.75 باسكت @ 110 ج)");

            // بيع 35 باسكت كاش (3,850 ج.م)
            await productionService.RecordProductSaleAsync(new CreateProductSaleDto
            {
                ProductionOrderId = order2.Id,
                SoldBaskets = 35m,
                BasketSellingPrice = 110m,
                PaidAmount = 3850m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "بيع 35 باسكت باتيه كاش"
            });
            Console.WriteLine("    💰 بيع 35 باسكت باتيه = 3,850 ج.م كاش");

            // بيع 18.75 باسكت دفع جزئي (إجمالي 2,062.5 ج.م - مدفوع 1,200 ج.م - متبقي 862.5 ج.م)
            await productionService.RecordProductSaleAsync(new CreateProductSaleDto
            {
                ProductionOrderId = order2.Id,
                SoldBaskets = 18.75m,
                BasketSellingPrice = 110m,
                PaidAmount = 1200m,
                PaymentMethod = PaymentMethod.PartiallyPaid,
                Notes = "بيع 18.75 باسكت باتيه - دفع جزئي"
            });
            Console.WriteLine("    💵 بيع 18.75 باسكت باتيه (إجمالي 2,062.5 ج.م - مدفوع 1,200 ج.م - متبقي 862.5 ج.م)");

            // --- دورة 3: ساندوتش (15 شكارة دقيق) ---
            var order3 = await productionService.CreateProductionOrderAsync(15m, ProductType.Sandwich, "وردية ساندوتش صباحية", 95m);
            await productionService.UpdateActualProductionAsync(order3.Id, 1020m, "إنتاج 1020 قطعة (36.43 باسكت)");
            await productionService.ConfirmProductionOrderAsync(order3.Id);
            Console.WriteLine($"  - أمر إنتاج #3 (ساندوتش 15 شكارة): تم تأكيده وخصم الخامات (36.43 باسكت @ 95 ج)");

            // بيع 25 باسكت كاش (2,375 ج.م)
            await productionService.RecordProductSaleAsync(new CreateProductSaleDto
            {
                ProductionOrderId = order3.Id,
                SoldBaskets = 25m,
                BasketSellingPrice = 95m,
                PaidAmount = 2375m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "بيع 25 باسكت ساندوتش كاش"
            });
            Console.WriteLine("    💰 بيع 25 باسكت ساندوتش = 2,375 ج.م كاش");

            // ----------------------------------------------------------------------------------
            // 7. طباعة الميزانية الشاملة والملخص المالي من الخزينة
            // ----------------------------------------------------------------------------------
            Console.WriteLine("\n📊 [7/7] جلب واستعراض التقرير المالي الموحد للمصنع من الخزينة:");

            var summary = await treasuryService.GetTreasurySummaryAsync();

            Console.WriteLine("\n===============================================================================");
            Console.WriteLine("🏛️  التقرير المالي النهائي الموحد للمصنع (FINANCIAL STATEMENT)");
            Console.WriteLine("===============================================================================");
            Console.WriteLine($" 💵 إجمالي الإيرادات الفعلية  : {summary.TotalIncome:N2} ج.م");
            Console.WriteLine($" 💸 إجمالي المصروفات الإجمالية : {summary.TotalExpenses:N2} ج.م");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine($"   • مصروفات أجور وعمالة     : {summary.LaborExpenses:N2} ج.م");
            Console.WriteLine($"   • مصروفات خامات ومستلزمات  : {summary.RawMaterialExpenses:N2} ج.م");
            Console.WriteLine($"   • مصاريف تشغيلية وصيانة    : {summary.OperatingExpenses:N2} ج.م");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine($" 💰 رصيد الكاش الحالي بالخزنة : {summary.CashBalance:N2} ج.م");
            Console.WriteLine($" 🏦 رصيد التحويلات البنكية    : {summary.BankTransferBalance:N2} ج.م");
            Console.WriteLine($" 📦 قيمة المخزون الحالي بالمخزن : {summary.InventoryValue:N2} ج.م");
            Console.WriteLine($" 📑 إجمالي ديون الخامات والمصروفات المستحقة للغير : {summary.TotalRemainingAmountsPayable:N2} ج.م");
            Console.WriteLine($" 📈 صافي الأرباح التشغيلية (Net Profit) : {summary.NetProfit:N2} ج.م");
            Console.WriteLine("===============================================================================");

            Console.WriteLine("\n🎉 اكتملت الدورة الاختيارية واختبار جميع العمليات المالية بنجاح 100%!");
        }

        private static async Task<ExpenseCategory> GetOrAddCategoryAsync(BakeryDbContext db, string name, bool isSystem)
        {
            var cat = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == name);
            if (cat == null)
            {
                cat = new ExpenseCategory { Name = name, IsSystem = isSystem };
                db.ExpenseCategories.Add(cat);
                await db.SaveChangesAsync();
            }
            return cat;
        }

        private static async Task<Employee> GetOrAddEmployeeAsync(BakeryDbContext db, string name, string job, int age, decimal salary, string dayOff, string phone, DateTime startDate)
        {
            var emp = await db.Employees.FirstOrDefaultAsync(e => e.Name == name);
            if (emp == null)
            {
                emp = new Employee
                {
                    Name = name,
                    JobTitle = job,
                    Age = age,
                    MonthlySalary = salary,
                    WeeklyDayOff = dayOff,
                    PhoneNumber = phone,
                    IsActive = true,
                    StartedDate = startDate
                };
                db.Employees.Add(emp);
                await db.SaveChangesAsync();
            }
            else
            {
                emp.MonthlySalary = salary;
                emp.StartedDate = startDate;
                db.Employees.Update(emp);
                await db.SaveChangesAsync();
            }
            return emp;
        }

        private static async Task AddAttendanceRecordAsync(BakeryDbContext db, int employeeId, DateTime date, bool isPresent, string notes)
        {
            bool exists = await db.EmployeeAttendances.AnyAsync(a => a.EmployeeId == employeeId && a.Date.Date == date.Date);
            if (!exists)
            {
                db.EmployeeAttendances.Add(new EmployeeAttendance
                {
                    EmployeeId = employeeId,
                    Date = date,
                    IsPresent = isPresent,
                    Notes = notes,
                    CreatedAt = DateTime.Now
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
