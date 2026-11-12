using Vernuko.Core;

namespace Vernuko.Inventory;

/// <summary>A product or consumable material (krem, jel, boya, şampuan ...).</summary>
public sealed class Product : Entity
{
    private Product()
    {
    }

    public Product(string name, string unit, long salePriceMinor, long costMinor, int minimumStock, string? sku = null)
    {
        var r = Update(name, unit, salePriceMinor, costMinor, minimumStock, sku);
        if (r.IsFailure)
        {
            throw new ArgumentException(r.Error.Message);
        }

        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Sku { get; private set; }

    /// <summary>Counting unit: "adet", "ml", "gr" ... Quantities are whole numbers of this unit.</summary>
    public string Unit { get; private set; } = "adet";

    public long SalePriceMinor { get; private set; }

    public long CostMinor { get; private set; }

    /// <summary>Low-stock warning threshold.</summary>
    public int MinimumStock { get; private set; }

    public bool IsActive { get; private set; }

    public static Result Validate(string? name, string? unit, long salePriceMinor, long costMinor, int minimumStock)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("product.name", "Ürün adı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 12)
        {
            return Error.Validation("product.unit", "Birim zorunludur (ör. adet, ml, gr).");
        }

        if (salePriceMinor < 0 || costMinor < 0)
        {
            return Error.Validation("product.price", "Fiyat negatif olamaz.");
        }

        return minimumStock < 0 ? Error.Validation("product.min", "Kritik stok seviyesi negatif olamaz.") : Result.Success();
    }

    public Result Update(string name, string unit, long salePriceMinor, long costMinor, int minimumStock, string? sku)
    {
        var v = Validate(name, unit, salePriceMinor, costMinor, minimumStock);
        if (v.IsFailure)
        {
            return v;
        }

        Name = name.Trim();
        Unit = unit.Trim();
        SalePriceMinor = salePriceMinor;
        CostMinor = costMinor;
        MinimumStock = minimumStock;
        Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();
        return Result.Success();
    }

    public void SetActive(bool active) => IsActive = active;
}

public enum StockMovementKind
{
    Received,
    Sold,
    Consumed,
    Adjusted,
    Returned,
    Reversal,
}

/// <summary>Append-only stock ledger; the stock level is always the sum of movements.</summary>
public sealed class StockMovement
{
    private StockMovement()
    {
    }

    private StockMovement(Guid productId, StockMovementKind kind, int quantityDelta, DateTime atUtc)
    {
        Id = Ids.New();
        ProductId = productId;
        Kind = kind;
        QuantityDelta = quantityDelta;
        AtUtc = atUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public StockMovementKind Kind { get; private set; }

    /// <summary>Signed change in stock (+ in, − out).</summary>
    public int QuantityDelta { get; private set; }

    public long? UnitCostMinor { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? ReversesMovementId { get; private set; }

    public string? Note { get; private set; }

    public Guid? UserId { get; private set; }

    public DateTime AtUtc { get; private set; }

    public static Result<StockMovement> Create(Guid productId, StockMovementKind kind, int quantity, long? unitCostMinor, Guid? appointmentId, Guid? customerId, string? note, Guid? userId, DateTime nowUtc)
    {
        if (kind == StockMovementKind.Reversal)
        {
            return Error.Validation("stock.kind", "Ters hareket yalnızca Reverse ile oluşturulabilir.");
        }

        if (kind == StockMovementKind.Adjusted)
        {
            if (quantity == 0)
            {
                return Error.Validation("stock.zero", "Düzeltme miktarı sıfır olamaz.");
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                return Error.Validation("stock.reason", "Stok düzeltmesi için açıklama zorunludur.");
            }
        }
        else if (quantity <= 0)
        {
            return Error.Validation("stock.quantity", "Miktar sıfırdan büyük olmalıdır.");
        }

        var delta = kind switch
        {
            StockMovementKind.Received or StockMovementKind.Returned => quantity,
            StockMovementKind.Sold or StockMovementKind.Consumed => -quantity,
            _ => quantity,
        };
        return new StockMovement(productId, kind, delta, Guard.Utc(nowUtc))
        {
            UnitCostMinor = unitCostMinor,
            AppointmentId = appointmentId,
            CustomerId = customerId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            UserId = userId,
        };
    }

    public static StockMovement Reverse(StockMovement original, string note, Guid? userId, DateTime nowUtc) =>
        new(original.ProductId, StockMovementKind.Reversal, -original.QuantityDelta, Guard.Utc(nowUtc))
        {
            AppointmentId = original.AppointmentId,
            CustomerId = original.CustomerId,
            ReversesMovementId = original.Id,
            Note = note,
            UserId = userId,
        };

    public static string KindName(StockMovementKind kind) => kind switch
    {
        StockMovementKind.Received => "Stok girişi",
        StockMovementKind.Sold => "Satış",
        StockMovementKind.Consumed => "Hizmette kullanım",
        StockMovementKind.Adjusted => "Sayım düzeltmesi",
        StockMovementKind.Returned => "İade",
        _ => "Ters hareket",
    };
}

/// <summary>Material automatically consumed when an appointment of the service is completed.</summary>
public sealed class ServiceMaterial
{
    private ServiceMaterial()
    {
    }

    public ServiceMaterial(Guid serviceId, Guid productId, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ServiceId = serviceId;
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid ServiceId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }
}
