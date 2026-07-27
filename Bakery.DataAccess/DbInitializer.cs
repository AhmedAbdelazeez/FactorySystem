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

            // 2. Seed Raw Materials in Warehouse
            var rawMaterials = await context.RawMaterials.ToListAsync();

            RawMaterial GetOrAddMaterial(string name, int unitId, decimal defaultQty, decimal unitPrice)
            {
                var mat = rawMaterials.FirstOrDefault(m => m.Name.Contains(name) || name.Contains(m.Name));
                if (mat == null)
                {
                    mat = new RawMaterial
                    {
                        Name = name,
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
                else if (mat.CurrentQuantity <= 0)
                {
                    mat.CurrentQuantity = defaultQty;
                    mat.UnitPrice = unitPrice;
                    mat.TotalValue = defaultQty * unitPrice;
                    mat.LastUpdatedDate = DateTime.Now;
                    context.RawMaterials.Update(mat);
                    context.SaveChanges();
                }
                return mat;
            }

            var matFlour = GetOrAddMaterial("شكاير دقيق", unitSack.Id, 50, 450);
            var matOil = GetOrAddMaterial("زيت", unitBottle.Id, 100, 60);
            var matSugar = GetOrAddMaterial("سكر", unitKg.Id, 200, 35);
            var matButter = GetOrAddMaterial("زبدة", unitKg.Id, 150, 120);
            var matPreservatives = GetOrAddMaterial("مواد حافظة", unitGram.Id, 15000, 0.5m);
            var matPackaging = GetOrAddMaterial("ورق تغليف", unitRoll.Id, 100, 25);
            var matImprover = GetOrAddMaterial("محسن", unitPiece.Id, 200, 15);
            var matYeast = GetOrAddMaterial("خميرة", unitPiece.Id, 100, 20);

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
                (matYeast, 2m),           // 2 قطعة خميرة
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
                    new Employee { Name = "أحمد محمود علي", JobTitle = "خباز أول", Age = 35, MonthlySalary = 8000, WeeklyDayOff = "الجمعة", PhoneNumber = "01012345678", Notes = "خبرة 10 سنوات في العجائن والفرن", IsActive = true },
                    new Employee { Name = "محمد عبد السلام", JobTitle = "عامل إنتاج", Age = 28, MonthlySalary = 5500, WeeklyDayOff = "الجمعة", PhoneNumber = "01123456789", Notes = "وردية صباحية - قسم التشكيل", IsActive = true },
                    new Employee { Name = "محمود حسن إبراهيم", JobTitle = "عامل تعبئة وتغليف", Age = 24, MonthlySalary = 4800, WeeklyDayOff = "الأحد", PhoneNumber = "01234567890", Notes = "مسؤول عن تعبئة الباسكيت والتغليف", IsActive = true },
                    new Employee { Name = "إبراهيم السيد أحمد", JobTitle = "فني صيانة", Age = 40, MonthlySalary = 6500, WeeklyDayOff = "الجمعة", PhoneNumber = "01545678901", Notes = "صيانة الأفران والعجانات", IsActive = true },
                    new Employee { Name = "السيد مصطفى حسين", JobTitle = "مشرف جودة", Age = 32, MonthlySalary = 7200, WeeklyDayOff = "الجمعة", PhoneNumber = "01098765432", Notes = "متابعة معايير الوزن والتصنيع", IsActive = true }
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
        }

        private static async Task<MeasurementUnit> AddUnitAsync(BakeryDbContext context, string name)
        {
            var unit = new MeasurementUnit { Name = name };
            context.MeasurementUnits.Add(unit);
            await context.SaveChangesAsync();
            return unit;
        }
    }
}
