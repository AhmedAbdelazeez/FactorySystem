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
        Task<ProductionCalculationResultDto> CalculateProductionTargetAsync(decimal flourSackCount, ProductType productType, decimal? customBasketPrice = null);
        Task<IEnumerable<ProductionOrderDto>> GetAllProductionOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<ProductionOrderDto?> GetProductionOrderByIdAsync(int id);
        Task<ProductionOrderDto> CreateProductionOrderAsync(decimal flourSackCount, ProductType productType, string? notes = null, decimal? customBasketPrice = null);
        Task<ProductionOrderDto> UpdateActualProductionAsync(int orderId, decimal actualQuantity, string? notes = null);
        Task ConfirmProductionOrderAsync(int orderId);
        Task DeleteProductionOrderAsync(int id);

        //new methods for product sales
        Task<bool> RecordProductSaleAsync(CreateProductSaleDto dto);
        Task<IEnumerable<ProductOrderSalesHistoryDto>> GetSalesHistoryAsync(DateTime? filterDate = null);
        Task<IEnumerable<ProductOrderSalesHistoryDto>> GetSalesHistoryByOrderIdAsync(int orderId);
        Task<IEnumerable<ProductionOrderDto>> GetAvailableOrdersForSaleAsync();
        Task<bool> CancelProductSaleAsync(int treasuryTransactionId);
        Task<bool> CollectRemainingSaleAmountAsync(int treasuryTransactionId, decimal amountToCollect, PaymentMethod paymentMethod, string? notes);
        //-------------------------------------------------------------------------
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
                .Include(r => r.RecipeItems).ThenInclude(i => i.RawMaterial).ThenInclude(m => m!.MeasurementUnit)
                .Include(r => r.RecipeItems).ThenInclude(i => i.RawMaterial).ThenInclude(m => m!.MaterialType)
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



        //  BL: تحديد عدد القطع لكل شكارة حسب النوع المختار
        private static decimal GetPiecesPerSackForType(ProductionSetting setting, ProductType type) => type switch
        {
            ProductType.Mabroum => setting.MabroumPiecesPerSack,
            ProductType.Pane => setting.PanePiecesPerSack,
            ProductType.Sandwich => setting.SandwichPiecesPerSack,
            _ => 0
        };


        private static string GetProductTypeName(ProductType type) => type switch
        {
            ProductType.Mabroum => "مبروم",
            ProductType.Pane => "بانيه",
            ProductType.Sandwich => "ساندوتش",
            _ => "غير محدد"
        };

        public async Task<ProductionCalculationResultDto> CalculateProductionTargetAsync(decimal flourSackCount, ProductType productType, decimal? customBasketPrice = null)
        {
            var setting = await GetProductionSettingsAsync();
            var recipe = await GetActiveRecipeAsync();

            var price = customBasketPrice ?? setting.BasketSellingPrice;

            decimal piecesPerSack = GetPiecesPerSackForType(setting, productType);
            int totalTargetPieces = (int)Math.Round(flourSackCount * piecesPerSack);

            int piecesPerBasket = setting.PiecesPerBasket > 0 ? setting.PiecesPerBasket : 28;

            decimal expectedBaskets = Math.Round((decimal)totalTargetPieces / piecesPerBasket, 2);
            int remainingPieces = totalTargetPieces % piecesPerBasket;

            decimal totalExpectedValue = expectedBaskets * price;

            var rawMaterials = await _context.RawMaterials
                .Include(r => r.MeasurementUnit)
                .Include(r => r.MaterialType)
                .ToDictionaryAsync(r => r.Id);

            var requiredMaterials = new List<RequiredMaterialDto>();
            foreach (var item in recipe.RecipeItems)
            {
                if (rawMaterials.TryGetValue(item.RawMaterialId, out var mat))
                {
                    decimal requiredPerSack = item.RequiredQuantity;

                    if (productType == ProductType.Pane)
                    {
                        if (mat.MaterialType?.Name == "زبدة")
                        {
                            requiredPerSack += 1;
                        }
                    }

                    decimal totalReq = requiredPerSack * flourSackCount;
                    requiredMaterials.Add(new RequiredMaterialDto
                    {
                        RawMaterialId = item.RawMaterialId,
                        MaterialName = mat.MaterialType != null ? mat.MaterialType.Name : "",
                        MeasurementUnitName = mat.MeasurementUnit?.Name ?? "",
                        RequiredQuantityPerSack = requiredPerSack,
                        TotalRequiredQuantity = totalReq,
                        AvailableQuantity = mat.CurrentQuantity
                    });
                }
            }

            return new ProductionCalculationResultDto
            {
                FlourSackCount = flourSackCount,
                ProductType = productType,
                ProductTypeName = GetProductTypeName(productType),
                TargetQuantity = totalTargetPieces,
                TotalTargetPieces = totalTargetPieces,
                ExpectedBaskets = expectedBaskets,
                RemainingPieces = remainingPieces,
                BasketSellingPrice = price,
                TotalExpectedSalesValue = totalExpectedValue,
                RequiredMaterials = requiredMaterials
            };
        }

        public async Task<ProductionOrderDto> CreateProductionOrderAsync(decimal flourSackCount, ProductType productType, string? notes = null, decimal? customBasketPrice = null)
        {
            if (flourSackCount <= 0)
                throw new InvalidOperationException("عدد شكاير الدقيق يجب أن يكون أكبر من الصفر.");

            if (!Enum.IsDefined(typeof(ProductType), productType))
                throw new InvalidOperationException("نوع المنتج المحدد غير صالح.");

            var calc = await CalculateProductionTargetAsync(flourSackCount, productType, customBasketPrice);

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
                SelectedProductType = productType,
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
                ProductType = productType,
                ProductTypeName = GetProductTypeName(productType),
                TargetQuantity = calc.TotalTargetPieces,
                ActualQuantity = 0,
                Difference = -calc.TotalTargetPieces,
                AchievementPercentage = 0
            });

            await _context.ProductionOrders.AddAsync(order);
            await _context.SaveChangesAsync();

            return (await GetProductionOrderByIdAsync(order.Id))!;
        }

        public async Task<ProductionOrderDto> UpdateActualProductionAsync(int orderId, decimal actualQuantity, string? notes = null)
        {
            var order = await _context.ProductionOrders.Include(o => o.OrderResults).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) throw new KeyNotFoundException("أمر الإنتاج غير موجود.");

            if (actualQuantity < 0)
                throw new InvalidOperationException("الكمية الفعلية لا يمكن أن تكون بالسالب.");

            var setting = await GetProductionSettingsAsync();
            int piecesPerBasket = setting.PiecesPerBasket > 0 ? setting.PiecesPerBasket : 28;

            var result = order.OrderResults.FirstOrDefault(r => r.ProductType == order.SelectedProductType);
            if (result != null)
            {
                result.ActualQuantity = (int)actualQuantity;
                result.Difference = (int)actualQuantity - result.TargetQuantity;
                result.AchievementPercentage = result.TargetQuantity > 0
                    ? Math.Round(actualQuantity / result.TargetQuantity * 100, 2) : 0;
            }

            order.TotalActualPieces = (int)actualQuantity;
            order.ActualBaskets = Math.Round(actualQuantity / piecesPerBasket, 2);
            order.RemainingPieces = (int)actualQuantity % piecesPerBasket;

            order.TotalActualSalesValue = order.ActualBaskets * order.BasketSellingPrice;
            if (!string.IsNullOrEmpty(notes)) order.Notes = notes;

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

            var calc = await CalculateProductionTargetAsync(order.FlourSackCount, order.SelectedProductType, order.BasketSellingPrice);
            var insufficientList = calc.RequiredMaterials.Where(m => !m.IsSufficient).ToList();

            if (insufficientList.Any())
            {
                var errorMsgs = string.Join("، ", insufficientList.Select(m => $"{m.MaterialName} (المتاح: {m.AvailableQuantity} {m.MeasurementUnitName} - المطلوب: {m.TotalRequiredQuantity} {m.MeasurementUnitName})"));
                throw new InvalidOperationException($"عفواً، لا يمكن تأكيد الإنتاج لعدم كفاية المواد الخام التالية في المخزن: {errorMsgs}");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var req in calc.RequiredMaterials)
                {
                    await _inventoryService.DeductStockAsync(
                        req.RawMaterialId,
                        req.TotalRequiredQuantity,
                        order.Id,
                        $"خصم خامات لأمر إنتاج رقم {order.Id} (عدد {order.FlourSackCount} شكارة - {GetProductTypeName(order.SelectedProductType)})"
                    );
                }

                order.Status = ProductionStatus.Confirmed;
                order.ConfirmedAt = DateTime.Now;
                _context.ProductionOrders.Update(order);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        //Method for productsales
        public async Task<bool> RecordProductSaleAsync(CreateProductSaleDto dto)
        {
            var order = await _context.ProductionOrders
                .FirstOrDefaultAsync(o => o.Id == dto.ProductionOrderId && o.Status == ProductionStatus.Confirmed);

            if (order == null)
                throw new InvalidOperationException("أمر الإنتاج المكتوب غير موجود أو غير مؤكد للبيع.");

            if (dto.SoldBaskets <= 0)
                throw new InvalidOperationException("عدد الباسكيت المباع يجب أن يكون أكبر من الصفر.");

            if (dto.SoldBaskets > order.ActualBaskets + 0.001m)
                throw new InvalidOperationException($"الكمية المطلوبة ({dto.SoldBaskets}) أكبر من المتاح حالياً بالإنتاج ({order.ActualBaskets} باسكت).");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal totalSaleAmount = dto.SoldBaskets * dto.BasketSellingPrice;
                decimal paidAmount = dto.PaidAmount;

                PaymentMethod actualPaymentMethod;
                if (dto.PaidAmount >= totalSaleAmount)
                {
                    actualPaymentMethod = dto.PaymentMethod == PaymentMethod.BankTransfer ? PaymentMethod.BankTransfer : PaymentMethod.Cash;
                    paidAmount = totalSaleAmount;
                }
                else if (dto.PaidAmount > 0 && dto.PaidAmount < totalSaleAmount)
                {
                    actualPaymentMethod = PaymentMethod.PartiallyPaid;
                }
                else
                {
                    actualPaymentMethod = PaymentMethod.Unpaid;
                }

                decimal remainingAmount = totalSaleAmount - paidAmount;

                order.ActualBaskets = Math.Max(0, order.ActualBaskets - dto.SoldBaskets);
                _context.ProductionOrders.Update(order);
                await _context.SaveChangesAsync();

                var treasuryTx = new TreasuryTransaction
                {
                    Date = DateTime.Now,
                    TransactionName = $"بيع {dto.SoldBaskets} باسكت من أمر إنتاج رقم {order.Id} ({GetProductTypeName(order.SelectedProductType)})",
                    TransactionType = TreasuryTransactionType.Income,
                    Category = "مبيعات إنتاج",
                    Amount = totalSaleAmount,
                    PaymentMethod = actualPaymentMethod,
                    PaidAmount = paidAmount,
                    RemainingAmount = remainingAmount > 0 ? remainingAmount : 0,
                    Notes = dto.Notes,
                    ProductionOrderId = order.Id,
                    SoldBaskets = dto.SoldBaskets
                };

                await _treasuryRepo.AddAsync(treasuryTx);
                await _treasuryRepo.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<IEnumerable<ProductionOrderDto>> GetAvailableOrdersForSaleAsync()
        {
            var list = await _context.ProductionOrders
                .Include(o => o.OrderResults)
                .Where(o => o.Status == ProductionStatus.Confirmed && o.ActualBaskets > 0)
                .OrderByDescending(o => o.ProductionDate)
                .ToListAsync();

            return list.Select(MapToDto);
        }

        public async Task<IEnumerable<ProductOrderSalesHistoryDto>> GetSalesHistoryAsync(DateTime? filterDate = null)
        {
            var query = from t in _context.TreasuryTransactions
                        join o in _context.ProductionOrders on t.ProductionOrderId equals o.Id into orders
                        from o in orders.DefaultIfEmpty()
                        where t.TransactionType == TreasuryTransactionType.Income
                           && t.Category == "مبيعات إنتاج"
                           && t.SoldBaskets.HasValue && t.SoldBaskets.Value > 0
                        select new { t, o };

            if (filterDate.HasValue)
            {
                var start = filterDate.Value.Date;
                var end = start.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.t.Date >= start && x.t.Date <= end);
            }

            var rawSales = await query.OrderByDescending(x => x.t.Date).ThenByDescending(x => x.t.Id).ToListAsync();
            if (!rawSales.Any()) return new List<ProductOrderSalesHistoryDto>();

            var orderIds = rawSales.Select(x => x.t.ProductionOrderId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            var allOrderSales = await _context.TreasuryTransactions
                .Where(t => t.ProductionOrderId.HasValue && orderIds.Contains(t.ProductionOrderId.Value)
                         && t.TransactionType == TreasuryTransactionType.Income && t.Category == "مبيعات إنتاج")
                .OrderBy(t => t.Date).ThenBy(t => t.Id)
                .Select(t => new { t.Id, t.ProductionOrderId, t.SoldBaskets, t.Date })
                .ToListAsync();

            var resultList = new List<ProductOrderSalesHistoryDto>();

            foreach (var item in rawSales)
            {
                var t = item.t;
                var o = item.o;
                decimal currentSold = t.SoldBaskets ?? 0;

                decimal totalActualBaskets = 0;
                decimal priorAndCurrentSold = 0;

                if (o != null)
                {
                    decimal totalSoldAllTime = allOrderSales.Where(s => s.ProductionOrderId == o.Id).Sum(s => s.SoldBaskets ?? 0);
                    totalActualBaskets = o.ActualBaskets + totalSoldAllTime;

                    priorAndCurrentSold = allOrderSales
                        .Where(s => s.ProductionOrderId == o.Id && (s.Date < t.Date || (s.Date == t.Date && s.Id <= t.Id)))
                        .Sum(s => s.SoldBaskets ?? 0);
                }

                decimal remainingAtThisPoint = Math.Max(0m, totalActualBaskets - priorAndCurrentSold);

                resultList.Add(new ProductOrderSalesHistoryDto
                {
                    Id = t.Id,
                    ProductionOrderId = t.ProductionOrderId ?? 0,
                    ProductSoldAt = t.Date,
                    ProductType = o?.SelectedProductType ?? ProductType.Mabroum,
                    SelectedProductTypeName = o != null ? GetProductTypeName(o.SelectedProductType) : "",
                    FlourSackCount = o?.FlourSackCount ?? 0,
                    BasketSellingPrice = currentSold > 0 ? Math.Round(t.Amount / currentSold, 2) : (o?.BasketSellingPrice ?? 0),
                    TotalActualPieces = o?.TotalActualPieces ?? 0,
                    TotalActualBaskets = totalActualBaskets,
                    RemainingPieces = o?.RemainingPieces ?? 0,
                    RemainingBasketsInOrder = remainingAtThisPoint,
                    SoldBaskets = currentSold,
                    PaidAmount = t.PaidAmount,
                    PaymentMethod = t.PaymentMethod,
                    Notes = t.Notes
                });
            }

            return resultList;
        }


        public async Task<IEnumerable<ProductOrderSalesHistoryDto>> GetSalesHistoryByOrderIdAsync(int orderId)
        {
            var order = await _context.ProductionOrders.FirstOrDefaultAsync(o => o.Id == orderId);

            var allSalesForOrder = await _context.TreasuryTransactions
                .Where(t => t.ProductionOrderId == orderId
                         && t.TransactionType == TreasuryTransactionType.Income
                         && t.Category == "مبيعات إنتاج")
                .OrderBy(t => t.Date).ThenBy(t => t.Id)
                .ToListAsync();

            decimal totalSoldAllTime = allSalesForOrder.Sum(s => s.SoldBaskets ?? 0);
            decimal totalActualBaskets = (order?.ActualBaskets ?? 0) + totalSoldAllTime;

            var result = new List<ProductOrderSalesHistoryDto>();
            

            foreach (var t in allSalesForOrder.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id))
            {
                decimal currentSold = t.SoldBaskets ?? 0;

                // بما إننا شغالين على ترتيب تنازلي، بنحسب "المتبقي وقتها" بنفس منطق الدالة التانية
                decimal priorAndCurrentSold = allSalesForOrder
                    .Where(s => s.Date < t.Date || (s.Date == t.Date && s.Id <= t.Id))
                    .Sum(s => s.SoldBaskets ?? 0);

                result.Add(new ProductOrderSalesHistoryDto
                {
                    Id = t.Id,
                    ProductionOrderId = orderId,
                    ProductSoldAt = t.Date,
                    ProductType = order != null ? order.SelectedProductType : ProductType.Mabroum,
                    SelectedProductTypeName = order != null ? GetProductTypeName(order.SelectedProductType) : "",
                    FlourSackCount = order != null ? order.FlourSackCount : 0,
                    BasketSellingPrice = currentSold > 0 ? t.Amount / currentSold : (order != null ? order.BasketSellingPrice : 0),
                    TotalActualBaskets = totalActualBaskets,
                    RemainingBasketsInOrder = Math.Max(0m, totalActualBaskets - priorAndCurrentSold),
                    SoldBaskets = currentSold,
                    PaidAmount = t.PaidAmount,
                    PaymentMethod = t.PaymentMethod,
                    Notes = t.Notes
                });
            }


            return result;
        }

        public async Task<bool> CancelProductSaleAsync(int treasuryTransactionId)
        {
            var transaction = await _context.TreasuryTransactions
                .FirstOrDefaultAsync(t => t.Id == treasuryTransactionId);

            if (transaction == null)
                throw new InvalidOperationException("حركة البيع غير موجودة.");

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. إرجاع الباسكت المباع إلى رصيد أمر الإنتاج
                if (transaction.ProductionOrderId.HasValue)
                {
                    var order = await _context.ProductionOrders
                        .FirstOrDefaultAsync(o => o.Id == transaction.ProductionOrderId.Value);

                    if (order != null)
                    {
                        decimal restoredBaskets = transaction.SoldBaskets ?? 0;
                        order.ActualBaskets += restoredBaskets;
                        _context.ProductionOrders.Update(order);
                    }

                    // 2. البحث عن جميع حركات الخزينة الفرعية (الخاصة بتحصيل المتبقي من هذه الحركة)
                    string searchTag = $"تحصيل متبقي للحركة #{transaction.Id}";

                    var relatedCollectionTxs = await _context.TreasuryTransactions
                        .Where(t => t.ProductionOrderId == transaction.ProductionOrderId.Value
                                 && t.Notes != null
                                 && t.Notes.Contains(searchTag))
                        .ToListAsync();

                    // استخدام RemoveRange من الـ DbContext مباشرة لتفادي خطأ IRepository
                    if (relatedCollectionTxs.Any())
                    {
                        _context.TreasuryTransactions.RemoveRange(relatedCollectionTxs);
                    }
                }

                // 3. حذف حركة البيع الرئيسية
                _treasuryRepo.Remove(transaction);

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();
                return true;
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> CollectRemainingSaleAmountAsync(int treasuryTransactionId, decimal amountToCollect, PaymentMethod paymentMethod, string? notes)
        {
            var transaction = await _context.TreasuryTransactions
                .FirstOrDefaultAsync(t => t.Id == treasuryTransactionId && t.Category == "مبيعات إنتاج");

            if (transaction == null)
                throw new InvalidOperationException("حركة البيع غير موجودة.");

            if (amountToCollect <= 0 || amountToCollect > transaction.RemainingAmount)
                throw new InvalidOperationException("المبلغ المحصل غير صالح أو يتجاوز المبلغ المتبقي.");

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                transaction.PaidAmount += amountToCollect;
                transaction.RemainingAmount -= amountToCollect;
                if (transaction.RemainingAmount == 0)
                {
                    transaction.PaymentMethod = paymentMethod;
                }
                _context.TreasuryTransactions.Update(transaction);

                var collectTx = new TreasuryTransaction
                {
                    Date = DateTime.Now,
                    TransactionName = $"تحصيل متبقي مبيعات لأمر رقم {transaction.ProductionOrderId}",
                    TransactionType = TreasuryTransactionType.Income,
                    Category = "تحصيل متبقي مبيعات",
                    Amount = amountToCollect,
                    PaymentMethod = paymentMethod,
                    PaidAmount = amountToCollect,
                    RemainingAmount = 0,
                    Notes = $"تحصيل متبقي للحركة #{transaction.Id} | {notes}",
                    ProductionOrderId = transaction.ProductionOrderId
                };

                await _treasuryRepo.AddAsync(collectTx);
                await _treasuryRepo.SaveChangesAsync();
                await _context.SaveChangesAsync();

                await dbTransaction.CommitAsync();
                return true;
            }
            catch
            {
                await dbTransaction.RollbackAsync();
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
                    throw new InvalidOperationException("لا يمكن حذف أمر إنتاج مؤكد بعد خصم المخزون .");
                }
                _context.ProductionOrders.Remove(order);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ProductionOrderDto>> GetAllProductionOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.ProductionOrders
                .Include(o => o.OrderResults)
                .Include(o => o.TreasuryTransactions)
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
                 .Include(o => o.TreasuryTransactions)
                 .FirstOrDefaultAsync(o => o.Id == id);

            return order != null ? MapToDto(order) : null;
        }

        private static ProductionOrderDto MapToDto(ProductionOrder order)
        {
            decimal soldBasketsCount = order.TreasuryTransactions?
                .Where(t => t.TransactionType == TreasuryTransactionType.Income
                         && t.Category == "مبيعات إنتاج")
                .Sum(s => s.SoldBaskets ?? 0m) ?? 0m;

            decimal actualRealizedSales = order.TreasuryTransactions?
                .Where(t => t.TransactionType == TreasuryTransactionType.Income
                         && t.Category == "مبيعات إنتاج")
                .Sum(s => (s.SoldBaskets ?? 0m) * order.BasketSellingPrice) ?? 0m;

            return new ProductionOrderDto
            {
                Id = order.Id,
                ProductionDate = order.ProductionDate,
                FlourSackCount = order.FlourSackCount,
                SelectedProductType = order.SelectedProductType,
                SelectedProductTypeName = GetProductTypeName(order.SelectedProductType),
                BasketSellingPrice = order.BasketSellingPrice,
                Status = order.Status,
                TotalTargetPieces = order.TotalTargetPieces,
                TotalActualPieces = order.TotalActualPieces,
                ExpectedBaskets = order.ExpectedBaskets,
                ActualBaskets = order.ActualBaskets,
                SoldBaskets = soldBasketsCount,

                RemainingPieces = order.RemainingPieces,
                TotalExpectedSalesValue = order.TotalExpectedSalesValue,
                TotalActualSalesValue = actualRealizedSales,
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
