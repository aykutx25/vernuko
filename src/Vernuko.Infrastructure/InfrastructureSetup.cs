using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Vernuko.Application;
using Vernuko.Application.Common;

namespace Vernuko.Infrastructure;

public static class InfrastructureSetup
{
    /// <summary>
    /// Wires the application for a real installation. Logs never contain customer names, phones or notes — only IDs,
    /// error types and counts (see SECURITY.md).
    /// </summary>
    public static IServiceCollection AddVernuko(this IServiceCollection services, AppEnvironment env)
    {
        services.AddVernukoPlatform(env);
        services.AddVernukoApplication(env.DatabasePath);
        return services;
    }

    /// <summary>Paths, device id, secret protection and logging — also for a connected computer (no local database).</summary>
    public static IServiceCollection AddVernukoPlatform(this IServiceCollection services, AppEnvironment env)
    {
        services.AddSingleton(env);
        services.AddSingleton<IAppEnvironment>(env);
        services.AddSingleton<IDeviceIdentity, DeviceIdentity>();
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        }
        else
        {
            services.AddSingleton<ISecretProtector>(new DevelopmentSecretProtector(Path.Combine(env.DataDirectory, "dev-secret.key")));
        }

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(Path.Combine(env.LogDirectory, "vernuko-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, fileSizeLimitBytes: 10 * 1024 * 1024)
            .CreateLogger();
        services.AddLogging(b => b.ClearProviders().AddSerilog(logger, dispose: true));
        return services;
    }
}

public static class SecretProtectors
{
    /// <summary>The protector for this platform (DPAPI on Windows), usable before the service container exists.</summary>
    public static ISecretProtector ForCurrentUser(AppEnvironment env) => OperatingSystem.IsWindows()
        ? new DpapiSecretProtector()
        : new DevelopmentSecretProtector(Path.Combine(env.DataDirectory, "dev-secret.key"));
}
