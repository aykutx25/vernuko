using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Persistence;
using Vernuko.Presentation;
using Vernuko.Security;

namespace Vernuko.Tests.Support;

public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

public sealed class TestEnvironment(string root) : IAppEnvironment
{
    public string DatabasePath => Path.Combine(DataDirectory, "vernuko.db");

    public string DataDirectory => root;

    public string SafetyDirectory => Path.Combine(root, "safety");

    public string TempDirectory => Path.Combine(root, "temp");

    public string DefaultBackupDirectory => Path.Combine(root, "backups");

    public string AttachmentsDirectory => Path.Combine(root, "attachments");

    public string AppVersion => "0.1.0-test";

    public bool IsDemo => false;
}

/// <summary>Simulates DPAPI: keys are bound to an "account" key; a different account cannot unprotect.</summary>
public sealed class TestSecretProtector(byte[]? accountKey = null) : ISecretProtector
{
    private readonly byte[] _key = accountKey ?? RandomNumberGenerator.GetBytes(32);

    public byte[] Protect(byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(_key, 16);
        gcm.Encrypt(nonce, plaintext, cipher, tag);
        return [.. nonce, .. tag, .. cipher];
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        var data = protectedData;
        var plain = new byte[data.Length - 28];
        using var gcm = new AesGcm(_key, 16);
        gcm.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
        return plain;
    }
}

public sealed class TestDevice(string hash = "test-device-hash") : IDeviceIdentity
{
    public string DeviceHash { get; set; } = hash;
}

/// <summary>A real Vernuko application stack on a real SQLite file in a temp folder.</summary>
public sealed class TestApp : IAsyncDisposable
{
    public const string AdminUser = "yonetici";
    public const string AdminPassword = "Guclu-Parola-2026";

    // Thursday 12 Nov 2026 07:00 UTC = 10:00 in Istanbul.
    public static readonly DateTimeOffset DefaultNow = new(2026, 11, 12, 7, 0, 0, TimeSpan.Zero);

    private TestApp(string root, ManualTimeProvider time, ISecretProtector protector, TestDevice device, LicensingOptions? licensing)
    {
        Root = root;
        Time = time;
        Env = new TestEnvironment(root);
        Directory.CreateDirectory(Env.SafetyDirectory);
        Directory.CreateDirectory(Env.TempDirectory);
        Device = device;
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<TimeProvider>(time);
        services.AddSingleton(new PasswordHasher(10_000));
        services.AddSingleton<IAppEnvironment>(Env);
        services.AddSingleton(protector);
        services.AddSingleton<IDeviceIdentity>(device);
        if (licensing is not null)
        {
            services.AddSingleton(licensing);
        }

        services.AddVernukoApplication(Env.DatabasePath);
        services.AddVernukoPresentation();
        services.AddSingleton<FakeDialogs>();
        services.AddSingleton<Vernuko.Presentation.Common.IDialogService>(sp => sp.GetRequiredService<FakeDialogs>());
        services.AddSingleton<FakePlatform>();
        services.AddSingleton<Vernuko.Presentation.Common.IPlatformServices>(sp => sp.GetRequiredService<FakePlatform>());
        Services = services.BuildServiceProvider();
    }

    public string Root { get; }

    public ManualTimeProvider Time { get; }

    public TestEnvironment Env { get; }

    public TestDevice Device { get; }

    public ServiceProvider Services { get; }

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();

    public static async Task<TestApp> CreateAsync(string? root = null, ManualTimeProvider? time = null, ISecretProtector? protector = null, TestDevice? device = null, LicensingOptions? licensing = null)
    {
        root ??= Path.Combine(Path.GetTempPath(), "vernuko-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var app = new TestApp(root, time ?? new ManualTimeProvider(DefaultNow), protector ?? new TestSecretProtector(), device ?? new TestDevice(), licensing);
        await app.Get<DatabaseInitializer>().InitializeAsync(app.Env.DatabasePath, app.Env.SafetyDirectory, CancellationToken.None);
        return app;
    }

    /// <summary>Standard "Güzellik ve Lazer Merkezi" setup + login as owner.</summary>
    public async Task<SetupResult> SetupBeautyAsync(bool enableBackup = true)
    {
        var hours = WeeklySchedule.DefaultBusinessWeek();
        var result = await Get<Vernuko.Application.Services.SetupService>().CompleteAsync(new SetupRequest(
            BusinessProfileId.BeautyAndLaser,
            "Işıl Güzellik ve Lazer",
            "0212 555 44 33",
            "İstanbul",
            TimeZones.Turkey,
            hours,
            [new SetupStaff("Selin Demir", "Lazer Uzmanı"), new SetupStaff("Derya Arslan", "Uzman")],
            [
                new SetupServiceItem("Lazer Epilasyon", "Lazer", 30, 100_000, 0, 10, ResourceKind.Room),
                new SetupServiceItem("Cilt Bakımı", "Cilt", 60, 150_000, 0, 10, ResourceKind.Room),
            ],
            [new SetupResource("Lazer Odası 1", ResourceKind.Room), new SetupResource("Lazer Odası 2", ResourceKind.Room), new SetupResource("Lazer Cihazı", ResourceKind.Device)],
            "Ayşe Yılmaz",
            AdminUser,
            AdminPassword,
            enableBackup,
            Path.Combine(Root, "backups")));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        await Get<LicenseService>().RefreshAsync();
        var login = await Get<AuthService>().LoginAsync(AdminUser, AdminPassword);
        Assert.True(login.IsSuccess, login.IsFailure ? login.Error.ToString() : null);
        return result.Value;
    }

    public async Task<Catalog> CatalogAsync()
    {
        var c = Get<CatalogService>();
        var services = (await c.GetServicesAsync()).Value;
        var staff = (await c.GetStaffAsync()).Value;
        var resources = (await c.GetResourcesAsync()).Value;
        return new Catalog(
            services.Single(s => s.Name == "Lazer Epilasyon"),
            services.Single(s => s.Name == "Cilt Bakımı"),
            staff[0],
            staff[1],
            resources.Single(r => r.Name == "Lazer Odası 1"),
            resources.Single(r => r.Name == "Lazer Odası 2"),
            resources.Single(r => r.Name == "Lazer Cihazı"));
    }

    public async Task<Guid> AddCustomerAsync(string first = "Ayşe", string last = "Kara", string phone = "0532 456 78 90")
    {
        var r = await Get<CustomerService>().CreateAsync(new Vernuko.Customers.CustomerDetails(first, last, phone), allowDuplicates: true);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.ToString() : null);
        return r.Value;
    }

    /// <summary>Local business time on 12 Nov 2026 (Istanbul).</summary>
    public static DateTime Local(int hour, int minute = 0, int day = 12) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Unspecified);

    public async Task<VernukoDbContext> DbAsync() => await Get<IDbContextFactory<VernukoDbContext>>().CreateDbContextAsync();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        SqliteSetup.ReleaseFileHandles();
    }

    public void DeleteFiles()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
        }
    }
}

public sealed record Catalog(ServiceDto Laser, ServiceDto SkinCare, StaffDto Selin, StaffDto Derya, ResourceDto Room1, ResourceDto Room2, ResourceDto LaserDevice);

public sealed class FakeDialogs : Vernuko.Presentation.Common.IDialogService
{
    public bool ConfirmAnswer { get; set; } = true;

    public Queue<string?> PromptAnswers { get; } = new();

    public List<string> Shown { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Evet", string cancelText = "Vazgeç", bool destructive = false)
    {
        Shown.Add(title + ": " + message);
        return Task.FromResult(ConfirmAnswer);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Shown.Add(title + ": " + message);
        return Task.CompletedTask;
    }

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null, bool required = true) =>
        Task.FromResult(PromptAnswers.Count > 0 ? PromptAnswers.Dequeue() : initialValue);

    public Task<string?> PickFolderAsync(string title, string? initialFolder) => Task.FromResult(initialFolder);

    public Task<string?> PickOpenFileAsync(string title, string filter) => Task.FromResult<string?>(null);

    public Task<string?> PickSaveFileAsync(string title, string defaultFileName, string filter) => Task.FromResult<string?>(null);
}

public sealed class FakePlatform : Vernuko.Presentation.Common.IPlatformServices
{
    public List<Uri> Opened { get; } = [];

    public string? Clipboard { get; private set; }

    public void OpenUri(Uri uri) => Opened.Add(uri);

    public void OpenFolder(string path)
    {
    }

    public void CopyToClipboard(string text) => Clipboard = text;

    public Task WriteTextFileAsync(string path, string content) => File.WriteAllTextAsync(path, content);

    public Task<string> ReadTextFileAsync(string path) => File.ReadAllTextAsync(path);
}
