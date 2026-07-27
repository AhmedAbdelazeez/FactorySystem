using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bakery.DataAccess;
using Bakery.DataAccess.Repositories;
using Bakery.Domain.Entities;
using Bakery.Domain.Enums;
using Bakery.Business.DTOs;

namespace Bakery.Business.Services
{
    public interface IProductionService
    {
        Task<ProductionCalculationResultDto> CalculateProductionTargetAsync(decimal flourSackCount, decimal? customBasketPrice = null);
        Task<IEnumerable<ProductionOrderDto>> GetAllProductionOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<ProductionOrderDto?> GetProductionOrderByIdAsync(int id);
        Task<ProductionOrderDto> CreateProductionOrderAsync(decimal flourSackCount, string? notes = null, decimal? customBasketPrice = null);
        Task<ProductionOrderDto> UpdateActualProductionAsync(int orderId, int actualMabroum, int actualPane, int actualSandwich, string? notes = null);
        Task ConfirmProductionOrderAsync(int orderId);
        Task DeleteProductionOrderAsync(int id);

        Task<ProductionRecipe> GetActiveRecipeAsync();
        Task SaveRecipeItemsAsync(List<ProductionRecipeItem> items);
        Task<ProductionSetting> GetProductionSettingsAsync();
        Task UpdateProductionSettingsAsync(ProductionSetting settings);
    }

    public class ProductionService : IProductionService
    {
        private readonly BakeryDbContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly IRepository<TreasuryTransaction> _treasuryRepo;

        public ProductionService(
            BakeryDbContext context,
            IInventoryService inventoryService,
            IRepository<TreasuryTransaction> treasuryRepo)
        {
            _context = context;
            _inventoryService = inventoryService;
            _treasuryRepo = treasuryRepo;
        }

        public async Task<ProductionSetting> GetProductionSettingsAsync()
        {
            var setting = await _context.ProductionSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new ProductionSetting
                {
                    MabroumPiecesPerSack = 56,
                    PanePiecesPerSack = 43,
                    SandwichPiecesPerSack = 51,
                    PiecesPerBasket = 28,
                    BasketSellingPrice = 100,
                    LastUpdatedDate = DateTime.Now
                };
                await _context.ProductionSettings.AddAsync(setting);
                await _context.SaveChangesAsync();
            }
            return setting;
        }

        public async Task UpdateProductionSettingsAsync(ProductionSetting settings)
        {
            var existing = await GetProductionSettingsAsync();
            existing.MabroumPiecesPerSack = settings.MabroumPiecesPerSack;
            existing.PanePiecesPerSack = settings.PanePiecesPerSack;
            existing.SandwichPiecesPerSack = settings.SandwichPiecesPerSack;
            existing.PiecesPerBasket = settings.PiecesPerBasket;
            existing.BasketSellingPrice = settings.BasketSellingPrice;
            existing.LastUpdatedDate = DateTime.Now;

            _context.ProductionSettings.Update(existing);
            await _context.SaveChangesAsync();
        }

        public async Task<ProductionRecipe> GetActiveRecipeAsync()
        {
            var recipe = await _context.ProductionRecipes
                .Include(r => r.RecipeItems)
                .ThenInclude(i => i.RawMaterial)
                .ThenInclude(m => m!.MeasurementUnit)
                .FirstOrDefaultAsync(r => r.IsActive);

            if (recipe == null)
            {
                recipe = new ProductionRecipe
                {
                    Name = "الوصفة القياسية لشكارة الدقيق",
                    FlourSackQuantity = 1,
                    IsActive = true
                };
                await _context.ProductionRecipes.AddAsync(recipe);
                await _context.SaveChangesAsync();
            }
            return recipe;
        }

        public async Task SaveRecipeItemsAsync(List<ProductionRecipeItem> items)
        {
            var recipe = await GetActiveRecipeAsync();

            // Clear old items and add new
            var existingItems = await _context.ProductionRecipeItems
                .Where(i => i.ProductionRecipeId == recipe.Id)
                .ToListAsync();

            _context.ProductionRecipeItems.RemoveRange(existingItems);

            foreach (var item in items)
            {
                if (item.RawMaterialId > 0 && item.RequiredQuantity > 0)
                {
                    _context.ProductionRecipeItems.Add(new ProductionRecipeItem
                    {
                        ProductionRecipeId = recipe.Id,
                        RawMaterialId = item.RawMaterialId,
                        RequiredQuantity = item.RequiredQuantity
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<ProductionCalculationResultDto> CalculateProductionTargetAsync(decimal flourSackCount, decimal? customBasketPrice = null)
        {
            var setting = await GetProductionSettingsAsync();
            var recipe = await GetActiveRecipeAsync();

            var price = customBasketPrice ?? setting.BasketSellingPrice;

            int mabroum = (int)Math.Round(flourSackCount * setting.MabroumPiecesPerSack);
            int pane = (int)Math.Round(flourSackCount * setting.PanePiecesPerSack);
            int sandwich = (int)Math.Round(flourSackCount * setting.SandwichPiecesPerSack);

            int totalTargetPieces = mabroum + pane + sandwich;
            int piecesPerBasket = setting.PiecesPerBasket > 0 ? setting.PiecesPerBasket : 28;

            int expectedBaskets = totalTargetPieces / piecesPerBasket;
            int remainingPieces = totalTargetPieces % piecesPerBasket;

            decimal totalExpectedValue = expectedBaskets * price;

            var rawMaterials = await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .ToDictionaryAsync(r => r.Id);

            var requiredMaterials = new List<RequiredMaterialDto>();
            foreach (var item in recipe.RecipeItems)
            {
                if (rawMaterials.TryGetValue(item.RawMaterialId, out var mat))
                {
                    decimal totalReq = item.RequiredQuantity * flourSackCount;
                    requiredMaterials.Add(new RequiredMaterialDto
                    {
                        RawMaterialId = item.RawMaterialId,
                        MaterialName = mat.Name,
                        MeasurementUnitName = mat.MeasurementUnit?.Name ?? "",
                        RequiredQuantityPerSack = item.RequiredQuantity,
                        TotalRequiredQuantity = totalReq,
                        AvailableQuantity = mat.CurrentQuantity
                    });
                }
            }

            return new ProductionCalculationResultDto
            {
                FlourSackCount = flourSackCount,
                TargetMabroum = mabroum,
                TargetPane = pane,
                TargetSandwich = sandwich,
                TotalTargetPieces = totalTargetPieces,
                ExpectedBaskets = expectedBaskets,
                RemainingPieces = remainingPieces,
                BasketSellingPrice = price,
                TotalExpectedSalesValue = totalExpectedValue,
                RequiredMaterials = requiredMaterials
            };
        }

        public async Task<ProductionOrderDto> CreateProductionOrderAsync(decimal flourSackCount, string? notes = null, decimal? customBasketPrice = null)
        {
            if (flourSackCount <= 0)
                throw new InvalidOperationException("عدد شكاير الدقيق يجب أن يكون أكبر من الصفر.");

            var calc = await CalculateProductionTargetAsync(flourSackCount, customBasketPrice);

            // Validation: Check stock availability before creating order
            var insufficientList = calc.RequiredMaterials.Where(m => !m.IsSufficient).ToList();
            if (insufficientList.Any())
            {
                var errorMsgs = string.Join("، ", insufficientList.Select(m => $"{m.MaterialName} (المتاح: {m.AvailableQuantity} {m.MeasurementUnitName} - المطلوب: {m.TotalRequiredQuantity} {m.MeasurementUnitName})"));
                throw new InvalidOperationException($"عفواً، لا يمكن بدء الإنتاج اليومي لعدم وجود مخزون كافٍ بالمخزن للمواد التالية: {errorMsgs}");
            }

            var order = new ProductionOrder
            {
                ProductionDate = DateTime.Today,
                FlourSackCount = flourSackCount,
                BasketSellingPrice = calc.BasketSellingPrice,
                Status = ProductionStatus.Draft,
                TotalTargetPieces = calc.TotalTargetPieces,
                TotalActualPieces = 0,
                ExpectedBaskets = calc.ExpectedBaskets,
                ActualBaskets = 0,
                RemainingPieces = calc.RemainingPieces,
                TotalExpectedSalesValue = calc.TotalExpectedSalesValue,
                TotalActualSalesValue = 0,
                Notes = notes,
                CreatedAt = DateTime.Now
            };

            order.OrderResults.Add(new ProductionOrderResult
            {
                ProductType = ProductType.Mabroum,
                ProductTypeName = "مبروم",
                TargetQuantity = calc.TargetMabroum,
                ActualQuantity = 0,
                Difference = -calc.TargetMabroum,
                AchievementPercentage = 0
            });

            order.OrderResults.Add(new ProductionOrderResult
            {
                ProductType = ProductType.Pane,
                ProductTypeName = "بانيه",
                TargetQuantity = calc.TargetPane,
                ActualQuantity = 0,
                Difference = -calc.TargetPane,
                AchievementPercentage = 0
            });

            order.OrderResults.Add(new ProductionOrderResult
            {
                ProductType = ProductType.Sandwich,
                ProductTypeName = "ساندوتش",
                TargetQuantity = calc.TargetSandwich,
                ActualQuantity = 0,
                Difference = -calc.TargetSandwich,
                AchievementPercentage = 0
            });

            await _context.ProductionOrders.AddAsync(order);
            await _context.SaveChangesAsync();

            return (await GetProductionOrderByIdAsync(order.Id))!;
        }

        public async Task<ProductionOrderDto> UpdateActualProductionAsync(int orderId, int actualMabroum, int actualPane, int actualSandwich, string? notes = null)
        {
            var order = await _context.ProductionOrders
                .Include(o => o.OrderResults)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new KeyNotFoundException("أمر الإنتاج غير موجود.");

            var setting = await GetProductionSettingsAsync();
            int piecesPerBasket = setting.PiecesPerBasket > 0 ? setting.PiecesPerBasket : 28;

            foreach (var res in order.OrderResults)
            {
                if (res.ProductType == ProductType.Mabroum)
                {
                    res.ActualQuantity = actualMabroum;
                    res.Difference = actualMabroum - res.TargetQuantity;
                    res.AchievementPercentage = res.TargetQuantity > 0 ? Math.Round((decimal)actualMabroum / res.TargetQuantity * 100, 2) : 0;
                }
                else if (res.ProductType == ProductType.Pane)
                {
                    res.ActualQuantity = actualPane;
                    res.Difference = actualPane - res.TargetQuantity;
                    res.AchievementPercentage = res.TargetQuantity > 0 ? Math.Round((decimal)actualPane / res.TargetQuantity * 100, 2) : 0;
                }
                else if (res.ProductType == ProductType.Sandwich)
                {
                    res.ActualQuantity = actualSandwich;
                    res.Difference = actualSandwich - res.TargetQuantity;
                    res.AchievementPercentage = res.TargetQuantity > 0 ? Math.Round((decimal)actualSandwich / res.TargetQuantity * 100, 2) : 0;
                }
            }

            int totalActual = actualMabroum + actualPane + actualSandwich;
            order.TotalActualPieces = totalActual;
            order.ActualBaskets = totalActual / piecesPerBasket;
            order.TotalActualSalesValue = order.ActualBaskets * order.BasketSellingPrice;
            if (!string.IsNullOrEmpty(notes))
                order.Notes = notes;

            _context.ProductionOrders.Update(order);
            await _context.SaveChangesAsync();

            return (await GetProductionOrderByIdAsync(order.Id))!;
        }

        public async Task ConfirmProductionOrderAsync(int orderId)
        {
            var order = await _context.ProductionOrders
                .Include(o => o.OrderResults)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new KeyNotFoundException("أمر الإنتاج غير موجود.");
            if (order.Status == ProductionStatus.Confirmed) throw new InvalidOperationException("أمر الإنتاج مؤكد بالفعل.");

            // Calculate required materials
            var calc = await CalculateProductionTargetAsync(order.FlourSackCount, order.BasketSellingPrice);
            var insufficientList = calc.RequiredMaterials.Where(m => !m.IsSufficient).ToList();

            if (insufficientList.Any())
            {
                var errorMsgs = string.Join("، ", insufficientList.Select(m => $"{m.MaterialName} (المتاح: {m.AvailableQuantity} {m.MeasurementUnitName} - المطلوب: {m.TotalRequiredQuantity} {m.MeasurementUnitName})"));
                throw new InvalidOperationException($"عفواً، لا يمكن تأكيد الإنتاج لعدم كفاية المواد الخام التالية في المخزن: {errorMsgs}");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Deduct inventory
                foreach (var req in calc.RequiredMaterials)
                {
                    await _inventoryService.DeductStockAsync(
                        req.RawMaterialId,
                        req.TotalRequiredQuantity,
                        order.Id,
                        $"خصم خامات لأمر إنتاج رقم {order.Id} (عدد {order.FlourSackCount} شكارة)"
                    );
                }

                // Update order status
                order.Status = ProductionStatus.Confirmed;
                order.ConfirmedAt = DateTime.Now;
                _context.ProductionOrders.Update(order);
                await _context.SaveChangesAsync();

                // Add Treasury Transaction for Income
                if (order.TotalActualSalesValue > 0)
                {
                    var treasuryTx = new TreasuryTransaction
                    {
                        Date = order.ProductionDate,
                        TransactionName = $"إيراد إنتاج فعلي لأمر رقم {order.Id} ({order.ActualBaskets} باسيكت)",
                        TransactionType = TreasuryTransactionType.Income,
                        Category = "مبيعات إنتاج",
                        Amount = order.TotalActualSalesValue,
                        PaymentMethod = PaymentMethod.Cash,
                        PaidAmount = order.TotalActualSalesValue,
                        RemainingAmount = 0,
                        Notes = $"عدد شكاير: {order.FlourSackCount} - إجمالي قطع: {order.TotalActualPieces}",
                        ProductionOrderId = order.Id
                    };
                    await _treasuryRepo.AddAsync(treasuryTx);
                    await _treasuryRepo.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteProductionOrderAsync(int id)
        {
            var order = await _context.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order != null)
            {
                if (order.Status == ProductionStatus.Confirmed)
                {
                    throw new InvalidOperationException("لا يمكن حذف أمر إنتاج مؤكد بعد خصم المخزون وتسجيل الإيرادات.");
                }
                _context.ProductionOrders.Remove(order);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ProductionOrderDto>> GetAllProductionOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.ProductionOrders
                .Include(o => o.OrderResults)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(o => o.ProductionDate >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(o => o.ProductionDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            var list = await query.OrderByDescending(o => o.ProductionDate).ThenByDescending(o => o.Id).ToListAsync();
            return list.Select(MapToDto);
        }

        public async Task<ProductionOrderDto?> GetProductionOrderByIdAsync(int id)
        {
            var order = await _context.ProductionOrders
                .Include(o => o.OrderResults)
                .FirstOrDefaultAsync(o => o.Id == id);

            return order == null ? null : MapToDto(order);
        }

        private static ProductionOrderDto MapToDto(ProductionOrder order)
        {
            return new ProductionOrderDto
            {
                Id = order.Id,
                ProductionDate = order.ProductionDate,
                FlourSackCount = order.FlourSackCount,
                BasketSellingPrice = order.BasketSellingPrice,
                Status = order.Status,
                TotalTargetPieces = order.TotalTargetPieces,
                TotalActualPieces = order.TotalActualPieces,
                ExpectedBaskets = order.ExpectedBaskets,
                ActualBaskets = order.ActualBaskets,
                RemainingPieces = order.RemainingPieces,
                TotalExpectedSalesValue = order.TotalExpectedSalesValue,
                TotalActualSalesValue = order.TotalActualSalesValue,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                ConfirmedAt = order.ConfirmedAt,
                OrderResults = order.OrderResults.Select(r => new ProductionOrderResultDto
                {
                    Id = r.Id,
                    ProductType = r.ProductType,
                    ProductTypeName = r.ProductTypeName,
                    TargetQuantity = r.TargetQuantity,
                    ActualQuantity = r.ActualQuantity,
                    Difference = r.Difference,
                    AchievementPercentage = r.AchievementPercentage
                }).ToList()
            };
        }
    }
}
