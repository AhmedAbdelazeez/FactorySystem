using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.Domain.Entities;

namespace Bakery.DataAccess
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(BakeryDbContext context)
        {
            // Ensure Database is created
            await context.Database.MigrateAsync();

            // 1. Seed Measurement Units if not present
            var units = await context.MeasurementUnits.ToListAsync();
            var unitKg = units.FirstOrDefault(u => u.Name == "كجم") ?? await AddUnitAsync(context, "كجم");
            var unitGram = units.FirstOrDefault(u => u.Name == "جرام") ?? await AddUnitAsync(context, "جرام");
            var unitSack = units.FirstOrDefault(u => u.Name == "شكارة") ?? await AddUnitAsync(context, "شكارة");
            var unitPiece = units.FirstOrDefault(u => u.Name == "قطعة") ?? await AddUnitAsync(context, "قطعة");
            var unitLiter = units.FirstOrDefault(u => u.Name == "لتر") ?? await AddUnitAsync(context, "لتر");
            var unitBottle = units.FirstOrDefault(u => u.Name == "زجاجة" || u.Name == "إزازة") ?? await AddUnitAsync(context, "زجاجة");
            var unitRoll = units.FirstOrDefault(u => u.Name == "رول") ?? await AddUnitAsync(context, "رول");
            var unitCarton = units.FirstOrDefault(u => u.Name == "كرتونة") ?? await AddUnitAsync(context, "كرتونة");

            //seed matrial types(lookup) if not present
            var materialTypes = await context.MaterialTypes.ToListAsync();
            var matFlourType = materialTypes.FirstOrDefault(mt => mt.Name == "شكاره دقيق") ?? await AddMaterialTypeAsync(context, "شكاره دقيق");
            var matOilType = materialTypes.FirstOrDefault(mt => mt.Name == "زيت") ?? await AddMaterialTypeAsync(context, "زيت");
            var matSugarType = materialTypes.FirstOrDefault(mt => mt.Name == "سكر") ?? await AddMaterialTypeAsync(context, "سكر");
            var matButterType = materialTypes.FirstOrDefault(mt => mt.Name == "زبدة") ?? await AddMaterialTypeAsync(context, "زبدة");
            var matPreservativesType = materialTypes.FirstOrDefault(mt => mt.Name == "مواد حافظة") ?? await AddMaterialTypeAsync(context, "مواد حافظة");
            var matPackagingType = materialTypes.FirstOrDefault(mt => mt.Name == "ورق تغليف") ?? await AddMaterialTypeAsync(context, "ورق تغليف");
            var matImproverType = materialTypes.FirstOrDefault(mt => mt.Name == "محسن") ?? await AddMaterialTypeAsync(context, "محسن");
            var matYeastType = materialTypes.FirstOrDefault(mt => mt.Name == "خميرة") ?? await AddMaterialTypeAsync(context, "خميرة");

            // 2. Seed Raw Materials in warehouse
            var rawMaterials = await context.RawMaterials.ToListAsync();

            RawMaterial GetOrAddMaterial(MaterialType type, int unitId, decimal defaultQty, decimal unitPrice)
            {
                var mat = rawMaterials.FirstOrDefault(m => m.MaterialTypeId == type.Id);
                if (mat == null)
                {
                    mat = new RawMaterial
                    {
                        MaterialTypeId = type.Id,
                        Name = type.Name,
                        MeasurementUnitId = unitId,
                        CurrentQuantity = defaultQty,
                        UnitPrice = unitPrice,
                        TotalValue = defaultQty * unitPrice,
                        LastUpdatedDate = DateTime.Now
                    };
                    context.RawMaterials.Add(mat);
                    context.SaveChanges();
                    rawMaterials.Add(mat);
                }
                else
                {
                    bool updated = false;
                    if (string.IsNullOrWhiteSpace(mat.Name))
                    {
                        mat.Name = type.Name;
                        updated = true;
                    }
                    if (mat.MaterialTypeId == matYeastType.Id && mat.MeasurementUnitId != unitCarton.Id)
                    {
                        mat.MeasurementUnitId = unitCarton.Id;
                        if (mat.UnitPrice <= 20) mat.UnitPrice = 400; // 20 قطعة * 20 ج.م = 400 ج.م للكرتونة
                        updated = true;
                    }
                    if (updated)
                    {
                        context.RawMaterials.Update(mat);
                        context.SaveChanges();
                    }
                }
                return mat;
            }


            var matFlour = GetOrAddMaterial(matFlourType, unitSack.Id, 0, 450);
            var matOil = GetOrAddMaterial(matOilType, unitBottle.Id, 0, 60);
            var matSugar = GetOrAddMaterial(matSugarType, unitKg.Id, 0, 35);
            var matButter = GetOrAddMaterial(matButterType, unitKg.Id, 0, 120);
            var matPreservatives = GetOrAddMaterial(matPreservativesType, unitGram.Id, 0, 0.5m);
            var matPackaging = GetOrAddMaterial(matPackagingType, unitRoll.Id, 0, 25);
            var matImprover = GetOrAddMaterial(matImproverType, unitPiece.Id, 0, 15);
            var matYeast = GetOrAddMaterial(matYeastType, unitCarton.Id, 0, 400);

            // 3. Seed Recipe Items for Flour Sack
            var recipe = await context.ProductionRecipes
                .Include(r => r.RecipeItems)
                .FirstOrDefaultAsync(r => r.IsActive);

            if (recipe == null)
            {
                recipe = new ProductionRecipe
                {
                    Name = "الوصفة القياسية لشكارة الدقيق",
                    FlourSackQuantity = 1,
                    IsActive = true
                };
                context.ProductionRecipes.Add(recipe);
                await context.SaveChangesAsync();
            }

            // Clear static or empty recipe items if required items are missing
            var requiredItems = new List<(RawMaterial mat, decimal qty)>
            {
                (matFlour, 1m),           // 1 شكارة دقيق
                (matSugar, 3m),           // 3 كجم سكر
                (matYeast, 0.1m),         // 0.1 كرتونة خميرة (أي قطعتين من إجمالي 20 قطعة بالكرتونة)
                (matOil, 1m),             // 1 زجاجة زيت
                (matPreservatives, 150m), // 150 جرام مواد حافظة
                (matImprover, 2m),        // 2 قطعة محسن
                (matButter, 3m)           // 3 كجم زبدة (3 زبدة وفي حالة الباتيه 2)
            };


            var existingItems = await context.ProductionRecipeItems
                .Where(i => i.ProductionRecipeId == recipe.Id)
                .ToListAsync();

            if (!existingItems.Any())
            {
                foreach (var req in requiredItems)
                {
                    context.ProductionRecipeItems.Add(new ProductionRecipeItem
                    {
                        ProductionRecipeId = recipe.Id,
                        RawMaterialId = req.mat.Id,
                        RequiredQuantity = req.qty
                    });
                }
                await context.SaveChangesAsync();
            }

            // 4. Seed 5 Employees
            var existingEmps = await context.Employees.ToListAsync();
            if (existingEmps.Count < 5)
            {
                var defaultEmployees = new List<Employee>
                {
                    new Employee { Name = "أحمد محمود علي", JobTitle = "خباز أول", Age = 35, MonthlySalary = 8000, WeeklyDayOff = "الجمعة", PhoneNumber = "01012345678", Notes = "خبرة 10 سنوات في العجائن والفرن", IsActive = true,StartedDate=new DateTime(2026, 7, 1)},
                    new Employee { Name = "محمد عبد السلام", JobTitle = "عامل إنتاج", Age = 28, MonthlySalary = 5500, WeeklyDayOff = "الجمعة", PhoneNumber = "01123456789", Notes = "وردية صباحية - قسم التشكيل", IsActive = true },
                    new Employee { Name = "محمود حسن إبراهيم", JobTitle = "عامل تعبئة وتغليف", Age = 24, MonthlySalary = 4800, WeeklyDayOff = "الأحد", PhoneNumber = "01234567890", Notes = "مسؤول عن تعبئة الباسكيت والتغليف", IsActive = true,StartedDate=new DateTime(2026, 7, 1) },
                    new Employee { Name = "إبراهيم السيد أحمد", JobTitle = "فني صيانة", Age = 40, MonthlySalary = 6500, WeeklyDayOff = "الجمعة", PhoneNumber = "01545678901", Notes = "صيانة الأفران والعجانات", IsActive = true,StartedDate=new DateTime(2026, 7, 1) },
                    new Employee { Name = "السيد مصطفى حسين", JobTitle = "مشرف جودة", Age = 32, MonthlySalary = 7200, WeeklyDayOff = "الجمعة", PhoneNumber = "01098765432", Notes = "متابعة معايير الوزن والتصنيع", IsActive = true ,StartedDate=new DateTime(2026, 7, 1)}
                };

                foreach (var emp in defaultEmployees)
                {
                    if (!existingEmps.Any(e => e.Name == emp.Name))
                    {
                        context.Employees.Add(emp);
                    }
                }
                await context.SaveChangesAsync();
            }

            // 5. Seed Suppliers & Invoices
            if (!await context.Suppliers.AnyAsync())
            {
                var s1 = new Supplier
                {
                    Name = "شركة النيل للدقيق والمطاحن",
                    Phone = "01001112233",
                    Notes = "المورد الرئيسي لشكاير الدقيق الفاخر",
                    CreatedAt = DateTime.Now.AddDays(-30)
                };
                s1.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matFlour.Id });
                s1.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matYeast.Id });

                var s2 = new Supplier
                {
                    Name = "مؤسسة الأهرام للزيوت والسكر",
                    Phone = "01122334455",
                    Notes = "تأمين الزيوت والسكر والزبدة الطبيعية",
                    CreatedAt = DateTime.Now.AddDays(-20)
                };
                s2.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matOil.Id });
                s2.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matSugar.Id });
                s2.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matButter.Id });

                var s3 = new Supplier
                {
                    Name = "شركة الروضة لمواد التغليف والمحسنات",
                    Phone = "01233445566",
                    Notes = "توريد ورق التغليف والمواد الحافظة والمحسنات",
                    CreatedAt = DateTime.Now.AddDays(-15)
                };
                s3.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matPackaging.Id });
                s3.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matPreservatives.Id });
                s3.SuppliedMaterials.Add(new SupplierRawMaterial { RawMaterialId = matImprover.Id });

                context.Suppliers.AddRange(s1, s2, s3);
                await context.SaveChangesAsync();

                // Seed sample invoice of 50,000 EGP for Supplier 1
                var inv1 = new SupplierInvoice
                {
                    SupplierId = s1.Id,
                    InvoiceNumber = "INV-20260801-101",
                    InvoiceDate = DateTime.Today.AddDays(-5),
                    TotalAmount = 50000,
                    PaidAmount = 30000,
                    RemainingAmount = 20000,
                    PaymentMethod = Bakery.Domain.Enums.PaymentMethod.PartiallyPaid,
                    Notes = "توريد شحنة دقيق وخميرة للمصنع",
                    CreatedAt = DateTime.Now.AddDays(-5)
                };
                inv1.Items.Add(new SupplierInvoiceItem
                {
                    RawMaterialId = matFlour.Id,
                    Quantity = 100,
                    UnitPrice = 450,
                    TotalAmount = 45000
                });
                inv1.Items.Add(new SupplierInvoiceItem
                {
                    RawMaterialId = matYeast.Id,
                    Quantity = 250,
                    UnitPrice = 20,
                    TotalAmount = 5000
                });
                context.SupplierInvoices.Add(inv1);
                await context.SaveChangesAsync();
            }
        }


        private static async Task<MeasurementUnit> AddUnitAsync(BakeryDbContext context, string name)
        {
            var unit = new MeasurementUnit { Name = name };
            context.MeasurementUnits.Add(unit);
            await context.SaveChangesAsync();
            return unit;
        }

        private static async Task<MaterialType> AddMaterialTypeAsync(BakeryDbContext context, string name)
        {
            var type = new MaterialType { Name = name };
            context.MaterialTypes.Add(type);
            await context.SaveChangesAsync();
            return type;
        }
        
    }
}
