using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Inventory;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>Products, stock levels (always derived from the append-only movement ledger), service materials and product sales.</summary>
public sealed class InventoryService(AppServiceDeps deps) : AppServiceBase(deps), IInventoryService
{
    public Task<Result<IReadOnlyList<ProductDto>>> GetProductsAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<ProductDto>>(Permission.InventoryView, async db => Result.Success(await LoadProductsAsync(db, includeInactive, ct)), ct);

    public Task<Result<Guid>> SaveProductAsync(ProductDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.InventoryManage, async db =>
        {
            var v = Product.Validate(dto.Name, dto.Unit, dto.SalePriceMinor, dto.CostMinor, dto.MinimumStock);
            if (v.IsFailure)
            {
                return v.Error;
            }

            Product? p;
            if (dto.Id == Guid.Empty)
            {
                p = new Product(dto.Name, dto.Unit, dto.SalePriceMinor, dto.CostMinor, dto.MinimumStock, dto.Sku);
                db.Products.Add(p);
            }
            else
            {
                p = await db.Products.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
                if (p is null)
                {
                    return Error.NotFound("Ürün");
                }

                var r = p.Update(dto.Name, dto.Unit, dto.SalePriceMinor, dto.CostMinor, dto.MinimumStock, dto.Sku);
                if (r.IsFailure)
                {
                    return r.Error;
                }
            }

            p.SetActive(dto.IsActive);
            await Audit.AppendAsync(db, "product.saved", "Product", p.Id, $"Ürün kaydedildi: {p.Name}", ct);
            return p.Id;
        }, ct);

    /// <summary>Goods received (stok girişi).</summary>
    public Task<Result<int>> ReceiveAsync(Guid productId, int quantity, long? unitCostMinor, string? note, CancellationToken ct = default) =>
        MoveAsync(Permission.InventoryManage, productId, StockMovementKind.Received, quantity, unitCostMinor, null, note, "stock.received", ct);

    /// <summary>Stock count correction (± with mandatory reason).</summary>
    public Task<Result<int>> AdjustAsync(Guid productId, int delta, string reason, CancellationToken ct = default) =>
        MoveAsync(Permission.InventoryManage, productId, StockMovementKind.Adjusted, delta, null, null, reason, "stock.adjusted", ct);

    /// <summary>
    /// Sells a product to a customer: stock out + receivable (+ optional immediate payment) + seller commission,
    /// atomically and idempotently.
    /// </summary>
    public Task<Result<Guid>> SellAsync(SellProductRequest r, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.ProductsSell, async db =>
        {
            var key = $"product-sale:{r.IdempotencyKey}";
            var existing = await db.Transactions.AsNoTracking().Where(t => t.IdempotencyKey == key).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (existing is { } id)
            {
                return id;
            }

            // A free sale has no charge row: the key is on the stock movement.
            var earlierMovement = await db.StockMovements.AsNoTracking().Where(m => EF.Property<string?>(m, PersistenceNames.SaleKey) == key).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
            if (earlierMovement is { } movementId)
            {
                return movementId;
            }

            if (r.Quantity is < 1 or > 10_000)
            {
                return Error.Validation("product.quantity", "Miktar 1 ile 10.000 arasında olmalıdır.");
            }

            if (r.UnitPriceMinor < 0)
            {
                return Error.Validation("product.price", "Fiyat negatif olamaz.");
            }

            var (settings, _) = await BusinessContext.LoadAsync(db, ct);
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == r.ProductId, ct);
            if (product is null || !product.IsActive)
            {
                return Error.Validation("product.missing", "Ürün bulunamadı veya pasif.");
            }

            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct);
            if (customer is null || customer.State != CustomerState.Active)
            {
                return Error.Validation("product.customer", "Müşteri bulunamadı veya aktif değil.");
            }

            var total = checked(r.UnitPriceMinor * r.Quantity);
            var movement = StockMovement.Create(product.Id, StockMovementKind.Sold, r.Quantity, product.CostMinor, null, customer.Id, null, UserId, UtcNow);
            if (movement.IsFailure)
            {
                return movement.Error;
            }

            db.StockMovements.Add(movement.Value);
            db.Entry(movement.Value).Property(PersistenceNames.SaleKey).CurrentValue = key;
            Guid saleId;
            if (total > 0)
            {
                var charge = CustomerTransaction.Create(Branch.DefaultId, customer.Id, TransactionKind.Charge, new Money(total, settings.Currency), null, false,
                    null, null, $"Ürün satışı: {product.Name} × {r.Quantity}", UserId, UtcNow, key);
                if (charge.IsFailure)
                {
                    return charge.Error;
                }

                db.Transactions.Add(charge.Value);
                saleId = charge.Value.Id;
                if (r.PayNowMethod is { } method)
                {
                    var pay = CustomerTransaction.Create(Branch.DefaultId, customer.Id, TransactionKind.Payment, new Money(total, settings.Currency), method, false,
                        null, null, $"Ürün satışı ödemesi: {product.Name}", UserId, UtcNow, $"product-sale-payment:{r.IdempotencyKey}");
                    if (pay.IsFailure)
                    {
                        return pay.Error;
                    }

                    db.Transactions.Add(pay.Value);
                }
            }
            else
            {
                saleId = movement.Value.Id;
            }

            if (r.SoldByStaffId is { } staffId)
            {
                await OperationsPosting.PostSaleCommissionAsync(db, CommissionBasis.ProductSold, saleId, staffId, product.Id, total, r.Quantity, $"Ürün satışı: {product.Name} × {r.Quantity}", UtcNow, ct);
            }

            await Audit.AppendAsync(db, "product.sold", "Product", product.Id, $"{product.Name} × {r.Quantity}, müşteri #{customer.Number}", ct);
            return saleId;
        }, ct);

    public Task<Result<IReadOnlyList<StockMovementDto>>> GetMovementsAsync(Guid productId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<StockMovementDto>>(Permission.InventoryView, async db =>
        {
            var rows = await db.StockMovements.AsNoTracking().Where(m => m.ProductId == productId).OrderByDescending(m => m.AtUtc).Take(500).ToListAsync(ct);
            var customerIds = rows.Where(r => r.CustomerId is not null).Select(r => r.CustomerId!.Value).Distinct().ToList();
            var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
            var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
            return rows.Select(m => new StockMovementDto(m.AtUtc, m.Kind, m.QuantityDelta, m.Note,
                m.CustomerId is { } c ? customers.GetValueOrDefault(c) : null,
                m.UserId is { } u ? users.GetValueOrDefault(u) : null)).ToList();
        }, ct);

    public Task<Result<IReadOnlyList<ServiceMaterialDto>>> GetServiceMaterialsAsync(Guid serviceId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<ServiceMaterialDto>>(Permission.InventoryView, async db =>
            await db.ServiceMaterials.AsNoTracking().Where(m => m.ServiceId == serviceId)
                .Join(db.Products, m => m.ProductId, p => p.Id, (m, p) => new ServiceMaterialDto(m.ServiceId, m.ProductId, p.Name, p.Unit, m.Quantity))
                .ToListAsync(ct), ct);

    /// <summary>Replaces the materials automatically consumed when an appointment of this service is completed.</summary>
    public Task<Result<bool>> SetServiceMaterialsAsync(Guid serviceId, IReadOnlyList<(Guid ProductId, int Quantity)> materials, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.InventoryManage, async db =>
        {
            if (materials.Any(m => m.Quantity <= 0) || materials.Select(m => m.ProductId).Distinct().Count() != materials.Count)
            {
                return Error.Validation("material.invalid", "Her ürün bir kez ve pozitif miktarla eklenmelidir.");
            }

            db.ServiceMaterials.RemoveRange(await db.ServiceMaterials.Where(m => m.ServiceId == serviceId).ToListAsync(ct));
            foreach (var (productId, quantity) in materials)
            {
                db.ServiceMaterials.Add(new ServiceMaterial(serviceId, productId, quantity));
            }

            await Audit.AppendAsync(db, "service_materials.saved", "Service", serviceId, $"{materials.Count} malzeme tanımlandı", ct);
            return true;
        }, ct);

    private Task<Result<int>> MoveAsync(Permission permission, Guid productId, StockMovementKind kind, int quantity, long? unitCost, Guid? customerId, string? note, string action, CancellationToken ct) =>
        WriteAsync<int>(permission, async db =>
        {
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct);
            if (product is null)
            {
                return Error.NotFound("Ürün");
            }

            var movement = StockMovement.Create(productId, kind, quantity, unitCost, null, customerId, note, UserId, UtcNow);
            if (movement.IsFailure)
            {
                return movement.Error;
            }

            db.StockMovements.Add(movement.Value);
            var before = await db.StockMovements.Where(m => m.ProductId == productId).SumAsync(m => m.QuantityDelta, ct);
            await Audit.AppendAsync(db, action, "Product", productId, $"{product.Name}: {StockMovement.KindName(kind)} {movement.Value.QuantityDelta:+0;-0} {product.Unit}", ct);
            return before + movement.Value.QuantityDelta;
        }, ct);

    internal static async Task<IReadOnlyList<ProductDto>> LoadProductsAsync(VernukoDbContext db, bool includeInactive, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);
        var stock = await db.StockMovements.AsNoTracking().GroupBy(m => m.ProductId).Select(g => new { g.Key, Qty = g.Sum(m => m.QuantityDelta) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        return products.Select(p => new ProductDto(p.Id, p.Name, p.Sku, p.Unit, p.SalePriceMinor, p.CostMinor, p.MinimumStock, p.IsActive, stock.GetValueOrDefault(p.Id))).ToList();
    }
}
