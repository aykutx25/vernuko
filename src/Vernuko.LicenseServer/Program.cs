using Microsoft.EntityFrameworkCore;
using Vernuko.LicenseServer;

if (args is ["generate-key", var folder])
{
    KeyTool.Generate(folder, Console.Out);
    return;
}

var app = LicenseServerApp.Build(args);
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<LicenseServerDb>().Database.MigrateAsync();
    _ = scope.ServiceProvider.GetRequiredService<ServerSecrets>(); // fail fast on missing/invalid secrets
}

await app.RunAsync();
