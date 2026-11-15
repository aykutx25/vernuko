using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Payments;
using Vernuko.ServiceDesk;

namespace Vernuko.Application.Services;

/// <summary>
/// Demo data for Vernuko Stok and Vernuko Teknik Servis, each in its OWN demo database (never the real one): a shop
/// or a repair service with fictional products, suppliers, sales and service tickets, so every program can be tried
/// and shown under its own name. Vernuko Randevu's demo is <see cref="DemoDataSeeder"/>.
/// </summary>
public sealed class ModeDemoSeeder(SetupService setup, AuthService auth, CustomerService customers, CatalogService catalog, InventoryService inventory, CommerceService commerce, ServiceDeskService desk, TimeProvider time)
{
    public async Task<Result> SeedAsync(BusinessMode mode, CancellationToken ct = default)
    {
        if (mode == BusinessMode.Randevu)
        {
            return Error.Validation("demo.mode", "Vernuko Randevu'nun demosu DemoDataSeeder ile hazırlanır.");
        }

        if (!await setup.IsCompletedAsync(ct))
        {
            var r = await setup.CompleteAsync(mode == BusinessMode.Stok ? StockSetup() : ServiceSetup(), ct);
            if (r.IsFailure)
            {
                return r.Error;
            }
        }

        var login = await auth.LoginAsync(DemoDataSeeder.DemoUsername, DemoDataSeeder.DemoPasswordFor(mode), ct);
        if (login.IsFailure)
        {
            return login;
        }

        var existing = await customers.SearchAsync(null, false, 1, ct);
        if (existing.IsSuccess && existing.Value.Count > 0)
        {
            return Result.Success();
        }

        return mode == BusinessMode.Stok ? await SeedShopAsync(ct) : await SeedServiceAsync(ct);
    }

    private static SetupRequest StockSetup() => new(
        BusinessProfileId.HardwareStore,
        "Demo Yıldız Hırdavat",
        "0224 000 00 00",
        "Nilüfer / Bursa",
        TimeZones.Turkey,
        WeeklySchedule.DefaultBusinessWeek(),
        [new SetupStaff("Okan Yıldız", "Satış"), new SetupStaff("Ece Kurt", "Depo")],
        [],
        [],
        "Demo Yönetici",
        DemoDataSeeder.DemoUsername,
        DemoDataSeeder.DemoPasswordFor(BusinessMode.Stok),
        false,
        null,
        IsDemo: true);

    private static SetupRequest ServiceSetup() => new(
        BusinessProfileId.PhoneRepair,
        "Demo Sinyal Teknik Servis",
        "0216 000 00 00",
        "Kadıköy / İstanbul",
        TimeZones.Turkey,
        WeeklySchedule.DefaultBusinessWeek(),
        [new SetupStaff("Okan Yıldız", "Teknisyen"), new SetupStaff("Elif Şahin", "Teknisyen")],
        [
            new SetupServiceItem("Arıza tespiti", "Tespit", 30, 25_000, 0, 0, null),
            new SetupServiceItem("Ekran değişimi", "Onarım", 60, 60_000, 0, 0, null),
            new SetupServiceItem("Batarya değişimi", "Onarım", 30, 40_000, 0, 0, null),
            new SetupServiceItem("Şarj soketi değişimi", "Onarım", 45, 45_000, 0, 0, null),
            new SetupServiceItem("Yazılım yükleme", "Yazılım", 30, 30_000, 0, 0, null),
        ],
        [],
        "Demo Yönetici",
        DemoDataSeeder.DemoUsername,
        DemoDataSeeder.DemoPasswordFor(BusinessMode.TeknikServis),
        false,
        null,
        IsDemo: true);

    private async Task<Result> SeedShopAsync(CancellationToken ct)
    {
        var people = await AddCustomersAsync([("Hakan", "Usta", "0532 111 22 33"), ("Murat", "Demir", "0544 222 33 44"), ("Selin", "Aydın", "0505 333 44 55")], ct);
        if (people.IsFailure)
        {
            return people.Error;
        }

        var wholesaler = await commerce.SaveSupplierAsync(new SupplierDto(Guid.Empty, "Demir Toptan Hırdavat", "Nilüfer", "1234567890", "0224 555 10 20", null, null, null, null, true, 0), ct);
        var building = await commerce.SaveSupplierAsync(new SupplierDto(Guid.Empty, "Ece Yapı Malzemeleri", null, null, "0224 555 30 40", null, null, null, null, true, 0), ct);
        if (wholesaler.IsFailure || building.IsFailure)
        {
            return wholesaler.IsFailure ? wholesaler.Error : building.Error;
        }

        // name, code, unit, price, minimum, barcode no, category, shelf, supplier, received, cost
        var catalogue = new (string Name, string Sku, string Unit, long Price, int Min, int Code, string Category, string Shelf, Guid? Supplier, int Qty, long Cost)[]
        {
            ("Matkap ucu seti, 13 parça", "M-13", "adet", 45_000, 5, 1, "El aletleri", "A-03", wholesaler.Value, 12, 25_000),
            ("Vida 4×40, 100'lü kutu", "V-440", "kutu", 12_000, 10, 2, "Bağlantı", "B-07", wholesaler.Value, 40, 6_000),
            ("Silikon, şeffaf 280 ml", "SL-280", "adet", 9_500, 12, 3, "Yapı kimyasalları", "C-11", building.Value, 48, 4_500),
            ("Boya rulosu, 25 cm", "BR-25", "adet", 8_500, 6, 4, "Boya", "D-02", building.Value, 18, 3_800),
            ("Su terazisi, 60 cm", "ST-60", "adet", 32_000, 4, 5, "Ölçüm", "A-09", wholesaler.Value, 5, 17_000),
            ("Şerit metre, 5 m", "SM-5", "adet", 14_000, 5, 6, "Ölçüm", "A-10", wholesaler.Value, 20, 7_000),
            ("İş eldiveni", "IE-01", "çift", 4_500, 20, 7, "İş güvenliği", "E-01", building.Value, 60, 1_800),
            ("Uzatma kablosu, 3'lü 5 m", "UK-35", "adet", 26_000, 3, 8, "Elektrik", "F-04", null, 4, 15_000),
        };
        var ids = new List<Guid>();
        foreach (var p in catalogue)
        {
            var saved = await inventory.SaveProductAsync(new ProductDto(Guid.Empty, p.Name, p.Sku, p.Unit, p.Price, p.Cost, p.Min, true, 0, Ean13("869000000" + p.Code.ToString("000", System.Globalization.CultureInfo.InvariantCulture)), 20, p.Category, p.Shelf, p.Supplier), ct);
            if (saved.IsFailure)
            {
                return saved.Error;
            }

            ids.Add(saved.Value);
        }

        // Goods receipts: the wholesaler's partly paid, the building supplier's on account; the cable came without a supplier.
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        foreach (var (supplier, no, pay) in new[] { (wholesaler.Value, "DTH2026004471", 500_000L), (building.Value, "EYM-118", 0L) })
        {
            var lines = catalogue.Select((p, i) => (p, i)).Where(x => x.p.Supplier == supplier).Select(x => new PurchaseLineRequest(ids[x.i], x.p.Qty, x.p.Cost, 20)).ToList();
            var receipt = await commerce.CreatePurchaseAsync(new CreatePurchaseRequest(supplier, no, today, lines, true, pay, pay > 0 ? PaymentMethod.BankTransfer : null, null, "demo-receipt-" + no), ct);
            if (receipt.IsFailure)
            {
                return receipt.Error;
            }
        }

        var cable = await inventory.ReceiveAsync(ids[7], catalogue[7].Qty, catalogue[7].Cost, "Açılış stoku", ct);
        if (cable.IsFailure)
        {
            return cable.Error;
        }

        // Today's counter: walk-in cash and card, a tradesman's credit (veresiye), and one return.
        var first = await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(ids[0], 2, 45_000), new(ids[2], 1, 9_500)], 0, 99_500, 0, 0, null, "demo-sale-1"), ct);
        var second = await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(ids[1], 6, 12_000)], 0, 0, 72_000, 0, null, "demo-sale-2"), ct);
        var credit = await commerce.CreateSaleAsync(new CreateSaleRequest(people.Value[0], [new(ids[4], 2, 32_000), new(ids[7], 1, 26_000)], 0, 30_000, 0, 0, "Şantiye için", "demo-sale-3"), ct);
        var gloves = await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(ids[6], 4, 4_500)], 0, 18_000, 0, 0, null, "demo-sale-4"), ct);
        foreach (var s in new[] { first, second, credit, gloves })
        {
            if (s.IsFailure)
            {
                return s.Error;
            }
        }

        var siliconLine = first.Value.Lines.Single(l => l.ProductId == ids[2]);
        var refund = await commerce.CreateReturnAsync(new CreateReturnRequest(first.Value.Id, [new(siliconLine.Id, 1)], 9_500, 0, 0, "Fazla alınmış", "demo-return-1"), ct);
        return refund.IsFailure ? refund.Error : Result.Success();
    }

    private async Task<Result> SeedServiceAsync(CancellationToken ct)
    {
        var people = await AddCustomersAsync(
            [("Serdar", "Aksoy", "0532 410 20 30"), ("Gizem", "Er", "0544 510 30 40"), ("Burak", "Tan", "0505 610 40 50"), ("Melis", "Kara", "0533 710 50 60"), ("Kaan", "Uslu", "0542 810 60 70"), ("Ayşe", "Kara", "0532 456 78 90")],
            ct);
        if (people.IsFailure)
        {
            return people.Error;
        }

        var supplier = await commerce.SaveSupplierAsync(new SupplierDto(Guid.Empty, "Mobil Parça Toptan", null, null, "0212 555 60 70", null, null, null, null, true, 0), ct);
        if (supplier.IsFailure)
        {
            return supplier.Error;
        }

        var parts = new (string Name, string Sku, long Price, long Cost, int Min, int Qty, int Code, string Category)[]
        {
            ("Ekran, Telsa T12", "EK-T12", 150_000, 90_000, 2, 4, 31, "Yedek parça"),
            ("Batarya, Telsa T9", "BT-T9", 65_000, 35_000, 2, 5, 32, "Yedek parça"),
            ("Şarj soketi, Type-C", "SS-TC", 25_000, 9_000, 5, 12, 33, "Yedek parça"),
            ("Kılıf, Telsa T12", "KL-T12", 15_000, 5_000, 5, 20, 34, "Aksesuar"),
            ("Şarj aleti, 20 W", "SA-20", 35_000, 18_000, 3, 8, 35, "Aksesuar"),
        };
        var ids = new List<Guid>();
        foreach (var p in parts)
        {
            var saved = await inventory.SaveProductAsync(new ProductDto(Guid.Empty, p.Name, p.Sku, "adet", p.Price, p.Cost, p.Min, true, 0, Ean13("869000000" + p.Code.ToString("000", System.Globalization.CultureInfo.InvariantCulture)), 20, p.Category, "Tezgâh", supplier.Value), ct);
            if (saved.IsFailure)
            {
                return saved.Error;
            }

            ids.Add(saved.Value);
        }

        var receipt = await commerce.CreatePurchaseAsync(new CreatePurchaseRequest(supplier.Value, "MPT-2026-5521", DateOnly.FromDateTime(time.GetLocalNow().Date),
            parts.Select((p, i) => new PurchaseLineRequest(ids[i], p.Qty, p.Cost, 20)).ToList(), true, 0, null, null, "demo-parts"), ct);
        if (receipt.IsFailure)
        {
            return receipt.Error;
        }

        var staff = (await catalog.GetStaffAsync(false, ct)).Value;
        Guid? okan = staff.FirstOrDefault(s => s.DisplayName == "Okan Yıldız")?.Id;
        Guid? elif = staff.FirstOrDefault(s => s.DisplayName == "Elif Şahin")?.Id;
        TicketIntakeRequest Intake(int customer, DeviceKind device, string brand, string model, string serial, string complaint, string? accessories, string? condition, Guid? technician) =>
            new(people.Value[customer], device, brand, model, serial, complaint, accessories, condition, null, false, null, null, technician);

        // 1. Repairing: price approved by phone, screen and labour on the ticket; the customer left the screen code.
        var t1 = await desk.OpenTicketAsync(new OpenTicketRequest(Intake(0, DeviceKind.Phone, "Telsa", "T12", "352099001761481", "Ekran kırık, dokunmatik çalışmıyor", "Kılıf", "Arka kapakta çizik", okan), "1234", "demo-t1"), ct);
        t1 = await Then(t1, id => desk.SetDiagnosisAsync(id, "Ekran paneli kırık; anakart sağlam.", null, ct));
        t1 = await Then(t1, id => desk.SetEstimateAsync(id, 210_000, 25_000, ct));
        t1 = await Then(t1, id => desk.RecordApprovalAsync(id, true, ApprovalChannel.Phone, "Serdar Aksoy", ct));
        t1 = await Then(t1, id => desk.AddPartAsync(id, ids[0], 1, null, ct));
        t1 = await Then(t1, id => desk.AddLaborAsync(id, "Ekran değişimi", 60_000, okan, ct));

        // 2. Waiting for the customer's approval.
        var t2 = await desk.OpenTicketAsync(new OpenTicketRequest(Intake(1, DeviceKind.Tablet, "Tabula", "T10", "R52N80ABC12", "Şarj olmuyor", "Şarj kablosu", null, elif), null, "demo-t2"), ct);
        t2 = await Then(t2, id => desk.ChangeStatusAsync(id, TicketStatus.Diagnosing, null, false, ct));
        t2 = await Then(t2, id => desk.SetDiagnosisAsync(id, "Şarj soketi oksitlenmiş.", null, ct));
        t2 = await Then(t2, id => desk.SetEstimateAsync(id, 70_000, 25_000, ct));

        // 3. Ready for pickup: battery replaced.
        var t3 = await desk.OpenTicketAsync(new OpenTicketRequest(Intake(2, DeviceKind.Phone, "Telsa", "T9", "356938035643809", "Batarya çabuk bitiyor", null, null, elif), null, "demo-t3"), ct);
        t3 = await Then(t3, id => desk.SetEstimateAsync(id, 105_000, 0, ct));
        t3 = await Then(t3, id => desk.RecordApprovalAsync(id, true, ApprovalChannel.InPerson, "Burak Tan", ct));
        t3 = await Then(t3, id => desk.AddPartAsync(id, ids[1], 1, null, ct));
        t3 = await Then(t3, id => desk.AddLaborAsync(id, "Batarya değişimi", 40_000, elif, ct));
        t3 = await Then(t3, id => desk.SetDiagnosisAsync(id, "Batarya kapasitesi %61.", "Batarya değiştirildi, şarj testi yapıldı.", ct));
        t3 = await Then(t3, id => desk.ChangeStatusAsync(id, TicketStatus.Ready, null, false, ct));

        // 4. Just received.
        var t4 = await desk.OpenTicketAsync(new OpenTicketRequest(Intake(3, DeviceKind.Phone, "Nova", "N5", "359812345678905", "Sıvı teması, açılmıyor", null, "Ekran sağlam, kasada nem izi", null), null, "demo-t4"), ct);

        // 5. Waiting for a part.
        var t5 = await desk.OpenTicketAsync(new OpenTicketRequest(Intake(5, DeviceKind.Phone, "Nova", "N7", "357001234567891", "Arka kamera bulanık çekiyor", "Kılıf", null, okan), null, "demo-t5"), ct);
        t5 = await Then(t5, id => desk.SetEstimateAsync(id, 120_000, 25_000, ct));
        t5 = await Then(t5, id => desk.RecordApprovalAsync(id, true, ApprovalChannel.Sms, "Ayşe Kara", ct));
        t5 = await Then(t5, id => desk.ChangeStatusAsync(id, TicketStatus.AwaitingParts, "Kamera modülü sipariş edildi.", false, ct));
        t5 = await Then(t5, id => desk.TakeDepositAsync(id, 50_000, PaymentMethod.Cash, "demo-t5-deposit", ct));

        // 6. Delivered and paid: its repair warranty runs.
        var t6 = await desk.OpenTicketAsync(new OpenTicketRequest(Intake(4, DeviceKind.Phone, "Telsa", "T12", "352099001761499", "Şarj soketi gevşek", null, null, okan), null, "demo-t6"), ct);
        t6 = await Then(t6, id => desk.AddPartAsync(id, ids[2], 1, null, ct));
        t6 = await Then(t6, id => desk.AddLaborAsync(id, "Şarj soketi değişimi", 45_000, okan, ct));
        t6 = await Then(t6, id => desk.SetDiagnosisAsync(id, "Soket pimleri aşınmış.", "Şarj soketi değiştirildi.", ct));
        t6 = await Then(t6, id => desk.ChangeStatusAsync(id, TicketStatus.Ready, null, false, ct));
        t6 = await Then(t6, id => desk.DeliverAsync(new DeliverTicketRequest(id, "Kaan Uslu", 0, 70_000, 0, "demo-t6-deliver"), ct));

        foreach (var t in new[] { t1, t2, t3, t4, t5, t6 })
        {
            if (t.IsFailure)
            {
                return t.Error;
            }
        }

        // An accessory sold at the counter.
        var sale = await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(ids[3], 1, 15_000), new(ids[4], 1, 35_000)], 0, 50_000, 0, 0, null, "demo-accessory"), ct);
        return sale.IsFailure ? sale.Error : Result.Success();
    }

    private static async Task<Result<TicketDto>> Then(Result<TicketDto> previous, Func<Guid, Task<Result<TicketDto>>> next) =>
        previous.IsFailure ? previous : await next(previous.Value.Summary.Id);

    private async Task<Result<IReadOnlyList<Guid>>> AddCustomersAsync(IReadOnlyList<(string First, string Last, string Phone)> people, CancellationToken ct)
    {
        var ids = new List<Guid>();
        foreach (var (first, last, phone) in people)
        {
            var c = await customers.CreateAsync(new CustomerDetails(first, last, phone), allowDuplicates: true, ct);
            if (c.IsFailure)
            {
                return c.Error;
            }

            ids.Add(c.Value);
        }

        return ids;
    }

    /// <summary>A valid EAN-13 from its first 12 digits (the demo's codes scan like real ones).</summary>
    public static string Ean13(string first12)
    {
        var sum = first12.Select((ch, i) => (ch - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return first12 + ((10 - (sum % 10)) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
