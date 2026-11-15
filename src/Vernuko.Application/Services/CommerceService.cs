using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.BusinessProfiles;
using Vernuko.Commerce;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Inventory;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>
/// Stok mode: counter sales and returns, goods receipts, suppliers and their accounts, stock counts, end of day.
/// Every money and stock effect is an append-only entry (customer ledger, supplier ledger, stock movements); nothing
/// is edited or deleted. Sales are internal records: the legal receipt comes from the business's ÖKC or e-Arşiv.
/// </summary>
public sealed class CommerceService(AppServiceDeps deps) : AppServiceBase(deps), ICommerceService
{
    internal const string WalkInCustomerKey = "commerce.walk_in_customer_id";

    // ------------------------------------------------------------------ products

    /// <summary>The product with this barcode or code (scanner input). Null when none.</summary>
    public Task<Result<ProductDto?>> FindProductAsync(string code, CancellationToken ct = default) =>
        ReadAsync<ProductDto?>(Permission.InventoryView, async db =>
        {
            var c = code?.Trim() ?? string.Empty;
            if (c.Length == 0)
            {
                return Result.Success<ProductDto?>(null);
            }

            var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Barcode == c, ct)
                ?? await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Sku != null && EF.Functions.Collate(x.Sku, "NOCASE") == c, ct);
            return Result.Success(p is null ? null : (ProductDto?)InventoryService.ToDto(p, await InventoryService.StockOfAsync(db, p.Id, ct)));
        }, ct);

    // ------------------------------------------------------------------ suppliers

    public Task<Result<IReadOnlyList<SupplierDto>>> GetSuppliersAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<SupplierDto>>(Permission.InventoryView, async db =>
        {
            var suppliers = await db.Suppliers.AsNoTracking().Where(s => includeInactive || s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);
            var balances = await db.SupplierTransactions.AsNoTracking().GroupBy(t => t.SupplierId).Select(g => new { g.Key, Sum = g.Sum(t => t.BalanceEffectMinor) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
            return Result.Success<IReadOnlyList<SupplierDto>>(suppliers.Select(s => ToDto(s, balances.GetValueOrDefault(s.Id))).ToList());
        }, ct);

    public Task<Result<Guid>> SaveSupplierAsync(SupplierDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.PurchasesManage, async db =>
        {
            var details = new SupplierDetails(dto.Name, dto.TaxOffice, dto.TaxNumber, dto.Phone, dto.Email, dto.Address, dto.Iban, dto.Note);
            Supplier? s;
            if (dto.Id == Guid.Empty)
            {
                var created = Supplier.Create(details);
                if (created.IsFailure)
                {
                    return created.Error;
                }

                s = created.Value;
                db.Suppliers.Add(s);
            }
            else
            {
                s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
                if (s is null)
                {
                    return Error.NotFound("Tedarikçi");
                }

                var r = s.Update(details);
                if (r.IsFailure)
                {
                    return r.Error;
                }
            }

            s.SetActive(dto.IsActive);
            await Audit.AppendAsync(db, dto.Id == Guid.Empty ? "supplier.created" : "supplier.updated", "Supplier", s.Id, s.Name, ct);
            return s.Id;
        }, ct);

    public Task<Result<SupplierStatementDto>> GetSupplierStatementAsync(Guid supplierId, CancellationToken ct = default) =>
        ReadAsync<SupplierStatementDto>(Permission.InventoryView, async db =>
        {
            var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, ct);
            if (s is null)
            {
                return Error.NotFound("Tedarikçi");
            }

            var entries = await db.SupplierTransactions.AsNoTracking().Where(t => t.SupplierId == supplierId).OrderBy(t => t.OccurredAtUtc).ThenBy(t => t.Id).ToListAsync(ct);
            var purchaseIds = entries.Where(e => e.PurchaseId != null).Select(e => e.PurchaseId!.Value).Distinct().ToList();
            var numbers = await db.Purchases.AsNoTracking().Where(p => purchaseIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Number, ct);
            long running = 0;
            var rows = new List<SupplierEntryDto>(entries.Count);
            foreach (var e in entries)
            {
                running += e.BalanceEffectMinor;
                rows.Add(new SupplierEntryDto(e.OccurredAtUtc, e.Kind, e.AmountMinor, e.BalanceEffectMinor, e.Method, e.Description,
                    e.PurchaseId is { } pid ? numbers.GetValueOrDefault(pid) : null, running));
            }

            return new SupplierStatementDto(ToDto(s, running), rows);
        }, ct);

    public Task<Result<Guid>> PaySupplierAsync(SupplierPaymentRequest r, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.PurchasesManage, async db =>
        {
            var key = $"supplier-payment:{r.IdempotencyKey}";
            if (await db.SupplierTransactions.AsNoTracking().Where(t => t.IdempotencyKey == key).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct) is { } done)
            {
                return done;
            }

            if (!await db.Suppliers.AsNoTracking().AnyAsync(s => s.Id == r.SupplierId, ct))
            {
                return Error.NotFound("Tedarikçi");
            }

            var tx = SupplierTransaction.Create(r.SupplierId, SupplierEntryKind.Payment, r.AmountMinor, r.Method, null, r.Note ?? "Tedarikçiye ödeme", UserId, UtcNow, key);
            if (tx.IsFailure)
            {
                return tx.Error;
            }

            db.SupplierTransactions.Add(tx.Value);
            await Audit.AppendAsync(db, "supplier.paid", "Supplier", r.SupplierId, $"{new Money(r.AmountMinor).ToDisplayString()} ({CustomerTransaction.MethodName(r.Method)})", ct);
            return tx.Value.Id;
        }, ct);

    // ------------------------------------------------------------------ sales

    public Task<Result<SaleDto>> CreateSaleAsync(CreateSaleRequest r, CancellationToken ct = default) =>
        WriteAsync<SaleDto>(Permission.ProductsSell, async db =>
        {
            var key = $"sale:{r.IdempotencyKey}";
            if (await db.Sales.AsNoTracking().Where(s => EF.Property<string?>(s, PersistenceNames.DocumentKey) == key).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct) is { } done)
            {
                return await LoadSaleAsync(db, done, ct);
            }

            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var walkIn = r.CustomerId is null;
            Guid customerId;
            if (walkIn)
            {
                customerId = await WalkInCustomerAsync(db, ct);
            }
            else
            {
                var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct);
                if (customer is null || customer.State != CustomerState.Active)
                {
                    return Error.Validation("sale.customer", "Müşteri bulunamadı veya aktif değil.");
                }

                customerId = customer.Id;
            }

            var ids = r.Lines.Select(l => l.ProductId).Distinct().ToList();
            var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            var inputs = new List<LineInput>(r.Lines.Count);
            foreach (var l in r.Lines)
            {
                if (!products.TryGetValue(l.ProductId, out var p) || !p.IsActive)
                {
                    return Error.Validation("sale.product", "Ürün bulunamadı veya pasif.");
                }

                inputs.Add(new LineInput(p.Id, p.Name, l.Quantity, l.UnitPriceMinor, p.VatRate, l.DiscountMinor, p.Barcode, p.CostMinor));
            }

            var number = await NextNumberAsync(db.Sales.Select(s => s.Number), DocumentNumbers.Sale, LocalYear(zone), ct);
            var created = Sale.Create(number, customerId, inputs, r.DocumentDiscountMinor, r.CashMinor, r.CardMinor, r.TransferMinor, allowOnAccount: !walkIn, r.Note, UserId, UtcNow);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var sale = created.Value;
            db.Sales.Add(sale);
            db.Entry(sale).Property(PersistenceNames.DocumentKey).CurrentValue = key;

            foreach (var line in sale.Lines)
            {
                var m = StockMovement.Create(line.ProductId, StockMovementKind.Sold, line.Quantity, line.UnitCostMinor, null, customerId, $"Satış {sale.Number}", UserId, UtcNow, sale.Id);
                db.StockMovements.Add(m.Value);
            }

            var money = (long amount) => new Money(amount, settings.Currency);
            if (sale.TotalMinor > 0)
            {
                Add(db, CustomerTransaction.Create(Branch.DefaultId, customerId, TransactionKind.Charge, money(sale.TotalMinor), null, false, null, null, $"Satış {sale.Number}", UserId, UtcNow, $"{key}:charge"));
            }

            foreach (var (amount, method) in new[] { (sale.CashMinor, PaymentMethod.Cash), (sale.CardMinor, PaymentMethod.Card), (sale.TransferMinor, PaymentMethod.BankTransfer) })
            {
                if (amount > 0)
                {
                    Add(db, CustomerTransaction.Create(Branch.DefaultId, customerId, TransactionKind.Payment, money(amount), method, false, null, null, $"Satış {sale.Number} ödemesi", UserId, UtcNow, $"{key}:pay:{method}"));
                }
            }

            await Audit.AppendAsync(db, "sale.created", "Sale", sale.Id, $"{sale.Number}: {new Money(sale.TotalMinor).ToDisplayString()}" + (sale.OnAccountMinor > 0 ? $", veresiye {new Money(sale.OnAccountMinor).ToDisplayString()}" : string.Empty), ct);
            await db.SaveChangesAsync(ct);
            return await LoadSaleAsync(db, sale.Id, ct);
        }, ct);

    public Task<Result<SaleDto>> CreateReturnAsync(CreateReturnRequest r, CancellationToken ct = default) =>
        WriteAsync<SaleDto>(Permission.PaymentsReverse, async db =>
        {
            var key = $"return:{r.IdempotencyKey}";
            if (await db.Sales.AsNoTracking().Where(s => EF.Property<string?>(s, PersistenceNames.DocumentKey) == key).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct) is { } done)
            {
                return await LoadSaleAsync(db, done, ct);
            }

            var original = await db.Sales.Include(s => s.Lines).AsNoTracking().FirstOrDefaultAsync(s => s.Id == r.SaleId, ct);
            if (original is null)
            {
                return Error.NotFound("Satış");
            }

            var returned = await ReturnedQuantitiesAsync(db, original.Id, ct);
            var inputs = new List<LineInput>();
            foreach (var req in r.Lines.Where(l => l.Quantity > 0))
            {
                var line = original.Lines.FirstOrDefault(l => l.Id == req.SaleLineId);
                if (line is null)
                {
                    return Error.Validation("return.line", "İade satırı bu satışa ait değil.");
                }

                var left = line.Quantity - returned.GetValueOrDefault(line.Id);
                if (req.Quantity > left)
                {
                    return Error.Validation("return.quantity", $"{line.ProductName}: en fazla {left} adet iade alınabilir.");
                }

                // The share of what the customer really paid for these units (after line and document discounts).
                var amount = (long)Math.Round((decimal)line.NetTotalMinor * req.Quantity / line.Quantity, MidpointRounding.AwayFromZero);
                var gross = line.UnitPriceMinor * req.Quantity;
                inputs.Add(new LineInput(line.ProductId, line.ProductName, req.Quantity, line.UnitPriceMinor, line.VatRate, gross - amount, line.Barcode, line.UnitCostMinor, line.Id));
            }

            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var walkIn = original.CustomerId == await WalkInCustomerAsync(db, ct);
            var number = await NextNumberAsync(db.Sales.Select(s => s.Number), DocumentNumbers.SaleReturn, LocalYear(zone), ct);
            var created = Sale.CreateReturn(number, original, inputs, r.CashMinor, r.CardMinor, r.TransferMinor, allowOnAccount: !walkIn, r.Note, UserId, UtcNow);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var ret = created.Value;
            db.Sales.Add(ret);
            db.Entry(ret).Property(PersistenceNames.DocumentKey).CurrentValue = key;
            foreach (var line in ret.Lines)
            {
                db.StockMovements.Add(StockMovement.Create(line.ProductId, StockMovementKind.Returned, line.Quantity, line.UnitCostMinor, null, original.CustomerId, $"İade {ret.Number} ({original.Number})", UserId, UtcNow, ret.Id).Value);
            }

            var money = (long amount) => new Money(amount, settings.Currency);
            if (ret.TotalMinor > 0)
            {
                Add(db, CustomerTransaction.Create(Branch.DefaultId, original.CustomerId, TransactionKind.Return, money(ret.TotalMinor), null, false, null, null, $"İade {ret.Number} ({original.Number})", UserId, UtcNow, $"{key}:return"));
            }

            foreach (var (amount, method) in new[] { (ret.CashMinor, PaymentMethod.Cash), (ret.CardMinor, PaymentMethod.Card), (ret.TransferMinor, PaymentMethod.BankTransfer) })
            {
                if (amount > 0)
                {
                    Add(db, CustomerTransaction.Create(Branch.DefaultId, original.CustomerId, TransactionKind.Refund, money(amount), method, false, null, null, $"İade {ret.Number} geri ödemesi", UserId, UtcNow, $"{key}:refund:{method}"));
                }
            }

            await Audit.AppendAsync(db, "sale.returned", "Sale", ret.Id, $"{ret.Number} ({original.Number}): {new Money(ret.TotalMinor).ToDisplayString()}", ct);
            await db.SaveChangesAsync(ct);
            return await LoadSaleAsync(db, ret.Id, ct);
        }, ct);

    public Task<Result<SaleDto>> GetSaleAsync(Guid saleId, CancellationToken ct = default) =>
        ReadAsync<SaleDto>(Permission.ProductsSell, db => LoadSaleAsync(db, saleId, ct), ct);

    public Task<Result<IReadOnlyList<SaleSummaryDto>>> GetSalesAsync(DateOnly from, DateOnly to, string? search = null, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<SaleSummaryDto>>(Permission.ProductsSell, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var start = TimeZones.DayRange(from, zone).Start;
            var end = TimeZones.DayRange(to, zone).End;
            var q = db.Sales.AsNoTracking().Where(s => s.OccurredAtUtc >= start && s.OccurredAtUtc < end);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var t = search.Trim();
                q = q.Where(s => s.Number.Contains(t));
            }

            var sales = await q.OrderByDescending(s => s.OccurredAtUtc).Take(1000).Select(s => new { s.Id, s.Number, s.Kind, s.OccurredAtUtc, s.CustomerId, s.TotalMinor, s.OnAccountMinor, Items = s.Lines.Sum(l => l.Quantity) }).ToListAsync(ct);
            var names = await CustomerNamesAsync(db, sales.Select(s => s.CustomerId), ct);
            return Result.Success<IReadOnlyList<SaleSummaryDto>>(sales.Select(s => new SaleSummaryDto(s.Id, s.Number, s.Kind, s.OccurredAtUtc, names.GetValueOrDefault(s.CustomerId, "?"), s.Items, s.TotalMinor, s.OnAccountMinor)).ToList());
        }, ct);

    // ------------------------------------------------------------------ goods receipts

    public Task<Result<PurchaseDto>> CreatePurchaseAsync(CreatePurchaseRequest r, CancellationToken ct = default) =>
        WriteAsync<PurchaseDto>(Permission.PurchasesManage, async db =>
        {
            var key = $"purchase:{r.IdempotencyKey}";
            if (await db.Purchases.AsNoTracking().Where(p => EF.Property<string?>(p, PersistenceNames.DocumentKey) == key).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct) is { } done)
            {
                return await LoadPurchaseAsync(db, done, ct);
            }

            var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == r.SupplierId, ct);
            if (supplier is null)
            {
                return Error.NotFound("Tedarikçi");
            }

            var ids = r.Lines.Select(l => l.ProductId).Distinct().ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            var inputs = new List<LineInput>(r.Lines.Count);
            foreach (var l in r.Lines)
            {
                if (!products.TryGetValue(l.ProductId, out var p))
                {
                    return Error.Validation("purchase.product", "Ürün bulunamadı.");
                }

                inputs.Add(new LineInput(p.Id, p.Name, l.Quantity, l.UnitCostMinor, l.VatRate, l.DiscountMinor));
            }

            if (r.PayNowMinor < 0)
            {
                return Error.Validation("purchase.pay", "Ödeme tutarı negatif olamaz.");
            }

            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var number = await NextNumberAsync(db.Purchases.Select(p => p.Number), DocumentNumbers.Purchase, LocalYear(zone), ct);
            var created = Purchase.Create(number, supplier.Id, r.DocumentNo, r.DocumentDate, inputs, r.Note, UserId, UtcNow);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var purchase = created.Value;
            if (r.PayNowMinor > purchase.TotalMinor)
            {
                return Error.Validation("purchase.overpaid", "Peşin ödeme belge toplamını geçemez; fazlası için ayrıca ödeme kaydedin.");
            }

            db.Purchases.Add(purchase);
            db.Entry(purchase).Property(PersistenceNames.DocumentKey).CurrentValue = key;
            foreach (var line in purchase.Lines)
            {
                db.StockMovements.Add(StockMovement.Create(line.ProductId, StockMovementKind.Received, line.Quantity, line.EffectiveUnitCostMinor, null, null, $"Mal kabul {purchase.Number} ({supplier.Name})", UserId, UtcNow, purchase.Id).Value);
                if (r.UpdateProductCosts)
                {
                    products[line.ProductId].SetCost(line.EffectiveUnitCostMinor);
                }
            }

            var debt = SupplierTransaction.Create(supplier.Id, SupplierEntryKind.Purchase, purchase.TotalMinor, null, purchase.Id, $"Alış {purchase.Number}" + (purchase.DocumentNo is null ? string.Empty : $" (belge {purchase.DocumentNo})"), UserId, UtcNow, $"{key}:debt");
            if (purchase.TotalMinor > 0)
            {
                db.SupplierTransactions.Add(debt.Value);
            }

            if (r.PayNowMinor > 0)
            {
                var pay = SupplierTransaction.Create(supplier.Id, SupplierEntryKind.Payment, r.PayNowMinor, r.PayNowMethod, purchase.Id, $"Alış {purchase.Number} ödemesi", UserId, UtcNow, $"{key}:pay");
                if (pay.IsFailure)
                {
                    return pay.Error;
                }

                db.SupplierTransactions.Add(pay.Value);
            }

            await Audit.AppendAsync(db, "purchase.created", "Purchase", purchase.Id, $"{purchase.Number} {supplier.Name}: {new Money(purchase.TotalMinor).ToDisplayString()}", ct);
            await db.SaveChangesAsync(ct);
            return await LoadPurchaseAsync(db, purchase.Id, ct);
        }, ct);

    public Task<Result<IReadOnlyList<PurchaseSummaryDto>>> GetPurchasesAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<PurchaseSummaryDto>>(Permission.InventoryView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var start = TimeZones.DayRange(from, zone).Start;
            var end = TimeZones.DayRange(to, zone).End;
            var rows = await db.Purchases.AsNoTracking().Where(p => p.OccurredAtUtc >= start && p.OccurredAtUtc < end).OrderByDescending(p => p.OccurredAtUtc)
                .Join(db.Suppliers, p => p.SupplierId, s => s.Id, (p, s) => new { p, s.Name, Items = p.Lines.Sum(l => l.Quantity) }).Take(1000).ToListAsync(ct);
            return Result.Success<IReadOnlyList<PurchaseSummaryDto>>(rows.Select(x => Summary(x.p, x.Name, x.Items)).ToList());
        }, ct);

    public Task<Result<PurchaseDto>> GetPurchaseAsync(Guid purchaseId, CancellationToken ct = default) =>
        ReadAsync<PurchaseDto>(Permission.InventoryView, db => LoadPurchaseAsync(db, purchaseId, ct), ct);

    // ------------------------------------------------------------------ stock count

    /// <summary>Counted quantities become correction movements (difference only); the earlier movements stay.</summary>
    public Task<Result<StockCountResult>> ApplyStockCountAsync(StockCountRequest r, CancellationToken ct = default) =>
        WriteAsync<StockCountResult>(Permission.InventoryManage, async db =>
        {
            var key = $"count:{r.IdempotencyKey}";
            if (await db.StockMovements.AsNoTracking().AnyAsync(m => EF.Property<string?>(m, PersistenceNames.SaleKey) != null && EF.Property<string?>(m, PersistenceNames.SaleKey)!.StartsWith(key), ct))
            {
                return Error.Conflict("count.done", "Bu sayım zaten uygulandı.");
            }

            if (r.Lines.Count == 0)
            {
                return Error.Validation("count.empty", "Sayılan en az bir ürün girin.");
            }

            if (r.Lines.Any(l => l.CountedQuantity < 0))
            {
                return Error.Validation("count.negative", "Sayılan miktar negatif olamaz.");
            }

            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var ids = r.Lines.Select(l => l.ProductId).Distinct().ToList();
            var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            var stock = await db.StockMovements.AsNoTracking().Where(m => ids.Contains(m.ProductId)).GroupBy(m => m.ProductId).Select(g => new { g.Key, Qty = g.Sum(m => m.QuantityDelta) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
            var label = $"Sayım {TimeZones.LocalDate(UtcNow, zone):dd.MM.yyyy}" + (string.IsNullOrWhiteSpace(r.Note) ? string.Empty : $" ({r.Note.Trim()})");
            int adjusted = 0, diffTotal = 0;
            long valueDiff = 0;
            var countId = Ids.New();
            foreach (var line in r.Lines.GroupBy(l => l.ProductId).Select(g => g.Last()))
            {
                if (!products.TryGetValue(line.ProductId, out var p))
                {
                    return Error.Validation("count.product", "Ürün bulunamadı.");
                }

                var current = stock.GetValueOrDefault(p.Id);
                var diff = line.CountedQuantity - current;
                if (diff == 0)
                {
                    continue;
                }

                var m = StockMovement.Create(p.Id, StockMovementKind.Adjusted, diff, p.CostMinor, null, null, $"{label}: kayıtta {current}, sayılan {line.CountedQuantity}", UserId, UtcNow, countId);
                db.StockMovements.Add(m.Value);
                db.Entry(m.Value).Property(PersistenceNames.SaleKey).CurrentValue = $"{key}:{p.Id}";
                adjusted++;
                diffTotal += diff;
                valueDiff += diff * p.CostMinor;
            }

            await Audit.AppendAsync(db, "stock.counted", "StockCount", countId, $"{label}: {r.Lines.Count} ürün sayıldı, {adjusted} üründe fark", ct);
            return new StockCountResult(r.Lines.Count, adjusted, diffTotal, valueDiff);
        }, ct);

    // ------------------------------------------------------------------ reports

    public Task<Result<DayEndDto>> GetDayEndAsync(DateOnly day, CancellationToken ct = default) =>
        ReadAsync<DayEndDto>(Permission.ReportsOperational, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var range = TimeZones.DayRange(day, zone);
            var docs = await db.Sales.AsNoTracking().Include(s => s.Lines).Where(s => s.OccurredAtUtc >= range.Start && s.OccurredAtUtc < range.End).ToListAsync(ct);
            var sales = docs.Where(d => d.Kind == SaleKind.Sale).ToList();
            var returns = docs.Where(d => d.Kind == SaleKind.Return).ToList();
            long Sign(Sale s) => s.Kind == SaleKind.Sale ? 1 : -1;
            var vat = docs.SelectMany(d => d.Lines.Select(l => (d, l))).GroupBy(x => x.l.VatRate).OrderBy(g => g.Key)
                .Select(g => new VatBreakdownDto(g.Key, g.Sum(x => Sign(x.d) * x.l.NetTotalMinor), g.Sum(x => Sign(x.d) * Vat.IncludedIn(x.l.NetTotalMinor, x.l.VatRate)))).ToList();
            var cost = docs.Sum(d => Sign(d) * d.Lines.Sum(l => l.UnitCostMinor * l.Quantity));
            return new DayEndDto(day, sales.Count, returns.Count, sales.Sum(s => s.TotalMinor), returns.Sum(s => s.TotalMinor),
                docs.Sum(d => Sign(d) * d.CashMinor), docs.Sum(d => Sign(d) * d.CardMinor), docs.Sum(d => Sign(d) * d.TransferMinor), docs.Sum(d => Sign(d) * d.OnAccountMinor), cost, vat);
        }, ct);

    /// <summary>Products at or below their critical level, grouped by usual supplier: refill to twice the critical level.</summary>
    public Task<Result<IReadOnlyList<ReorderGroupDto>>> GetReorderSuggestionsAsync(CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<ReorderGroupDto>>(Permission.InventoryView, async db =>
        {
            var products = await InventoryService.LoadProductsAsync(db, includeInactive: false, ct);
            var suppliers = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            var groups = products.Where(p => p.MinimumStock > 0 && p.Stock <= p.MinimumStock)
                .Select(p => new { p.SupplierId, Line = new ReorderLineDto(p.Id, p.Name, p.Barcode, p.Unit, p.Stock, p.MinimumStock, Math.Max(1, (p.MinimumStock * 2) - p.Stock), p.CostMinor) })
                .GroupBy(x => x.SupplierId)
                .Select(g => new ReorderGroupDto(g.Key, g.Key is { } id && suppliers.TryGetValue(id, out var n) ? n : "Tedarikçisi belirtilmemiş", g.Select(x => x.Line).OrderBy(l => l.ProductName).ToList()))
                .OrderBy(g => g.SupplierId is null).ThenBy(g => g.SupplierName)
                .ToList();
            return Result.Success<IReadOnlyList<ReorderGroupDto>>(groups);
        }, ct);

    public Task<Result<StockValueDto>> GetStockValueAsync(CancellationToken ct = default) =>
        ReadAsync<StockValueDto>(Permission.ReportsFinancial, async db =>
        {
            var products = await InventoryService.LoadProductsAsync(db, includeInactive: false, ct);
            var positive = products.Where(p => p.Stock > 0).ToList();
            return new StockValueDto(products.Count, positive.Sum(p => p.Stock), positive.Sum(p => (long)p.Stock * p.CostMinor), positive.Sum(p => (long)p.Stock * p.SalePriceMinor), products.Count(p => p.Stock < 0));
        }, ct);

    // ------------------------------------------------------------------ helpers

    /// <summary>The account kayıtsız (walk-in) sales are booked to; created on first use.</summary>
    internal static async Task<Guid> WalkInCustomerAsync(VernukoDbContext db, CancellationToken ct)
    {
        if (await SettingsStore.GetAsync(db, WalkInCustomerKey, ct) is { } stored && Guid.TryParse(stored, out var id) && await db.Customers.AsNoTracking().AnyAsync(c => c.Id == id, ct))
        {
            return id;
        }

        var number = (await db.Customers.MaxAsync(c => (int?)c.Number, ct) ?? 0) + 1;
        var customer = Customer.Create(number, new CustomerDetails("Perakende", "Satış", null, Notes: "Kayıtsız (perakende) satışlar bu hesaba yazılır. Kişisel bilgi içermez.")).Value;
        db.Customers.Add(customer);
        await SettingsStore.SetAsync(db, WalkInCustomerKey, customer.Id.ToString(), DateTime.UtcNow, ct);
        return customer.Id;
    }

    private static void Add(VernukoDbContext db, Result<CustomerTransaction> tx)
    {
        if (tx.IsFailure)
        {
            throw new InvalidOperationException(tx.Error.Message);
        }

        db.Transactions.Add(tx.Value);
    }

    private int LocalYear(TimeZoneInfo zone) => TimeZones.LocalDate(UtcNow, zone).Year;

    private static async Task<string> NextNumberAsync(IQueryable<string> numbers, string prefix, int year, CancellationToken ct)
    {
        var head = $"{prefix}-{year}-";
        var last = await numbers.Where(n => n.StartsWith(head)).OrderByDescending(n => n).FirstOrDefaultAsync(ct);
        return DocumentNumbers.Format(prefix, year, (last is null ? 0 : DocumentNumbers.SequenceOf(last, prefix, year)) + 1);
    }

    private static async Task<Dictionary<Guid, int>> ReturnedQuantitiesAsync(VernukoDbContext db, Guid saleId, CancellationToken ct) =>
        await db.SaleLines.AsNoTracking()
            .Where(l => l.ReturnOfLineId != null && db.Sales.Any(s => s.Id == l.SaleId && s.ReturnOfSaleId == saleId))
            .GroupBy(l => l.ReturnOfLineId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    private static async Task<Dictionary<Guid, string>> CustomerNamesAsync(VernukoDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Customers.AsNoTracking().Where(c => list.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CompanyName ?? (c.FirstName + " " + c.LastName), ct);
    }

    private async Task<Result<SaleDto>> LoadSaleAsync(VernukoDbContext db, Guid saleId, CancellationToken ct)
    {
        var s = await db.Sales.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == saleId, ct);
        if (s is null)
        {
            return Error.NotFound("Satış");
        }

        var names = await CustomerNamesAsync(db, [s.CustomerId], ct);
        var walkInId = await SettingsStore.GetAsync(db, WalkInCustomerKey, ct);
        var returned = s.Kind == SaleKind.Sale ? await ReturnedQuantitiesAsync(db, s.Id, ct) : [];
        var user = s.UserId is { } uid ? await db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) : null;
        var returnOf = s.ReturnOfSaleId is { } rid ? await db.Sales.AsNoTracking().Where(x => x.Id == rid).Select(x => x.Number).FirstOrDefaultAsync(ct) : null;
        var lines = s.Lines.OrderBy(l => l.LineNo).Select(l => new SaleLineDto(l.Id, l.LineNo, l.ProductId, l.ProductName, l.Barcode, l.Quantity, l.UnitPriceMinor, l.VatRate, l.DiscountMinor, l.TotalMinor, l.NetTotalMinor, returned.GetValueOrDefault(l.Id))).ToList();
        return new SaleDto(s.Id, s.Number, s.Kind, s.OccurredAtUtc, s.CustomerId, names.GetValueOrDefault(s.CustomerId, "?"), walkInId == s.CustomerId.ToString(),
            s.GrossMinor, s.LineDiscountMinor + s.DocumentDiscountMinor, s.TotalMinor, s.VatMinor, s.CashMinor, s.CardMinor, s.TransferMinor, s.OnAccountMinor, s.Note, user, lines, returnOf);
    }

    private static async Task<Result<PurchaseDto>> LoadPurchaseAsync(VernukoDbContext db, Guid purchaseId, CancellationToken ct)
    {
        var p = await db.Purchases.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == purchaseId, ct);
        if (p is null)
        {
            return Error.NotFound("Alış");
        }

        var supplier = await db.Suppliers.AsNoTracking().Where(s => s.Id == p.SupplierId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "?";
        var lines = p.Lines.OrderBy(l => l.LineNo).Select(l => new PurchaseLineDto(l.LineNo, l.ProductId, l.ProductName, l.Quantity, l.UnitCostMinor, l.VatRate, l.DiscountMinor, l.NetMinor, l.VatMinor)).ToList();
        return new PurchaseDto(Summary(p, supplier, p.Lines.Sum(l => l.Quantity)), p.SupplierId, p.Note, lines);
    }

    private static PurchaseSummaryDto Summary(Purchase p, string supplierName, int items) =>
        new(p.Id, p.Number, p.OccurredAtUtc, supplierName, p.DocumentNo, p.DocumentDate, items, p.NetMinor, p.VatMinor, p.TotalMinor);

    private static SupplierDto ToDto(Supplier s, long balance) =>
        new(s.Id, s.Name, s.TaxOffice, s.TaxNumber, s.Phone, s.Email, s.Address, s.Iban, s.Note, s.IsActive, balance);
}
