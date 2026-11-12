using Microsoft.Extensions.DependencyInjection;
using Vernuko.Presentation.Dialogs;
using Vernuko.Presentation.Pages;

namespace Vernuko.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddVernukoPresentation(this IServiceCollection services)
    {
        services.AddSingleton<RootViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<SetupWizardViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LockViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<AppointmentsViewModel>();
        services.AddTransient<AppointmentEditorViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<CatalogViewModel>();
        services.AddTransient<StaffViewModel>();
        services.AddTransient<PackagesViewModel>();
        services.AddTransient<PaymentsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<WaitlistViewModel>();
        services.AddTransient<CommissionRulesViewModel>();
        services.AddTransient<ConsentTemplatesViewModel>();
        services.AddTransient<CustomerRecordsViewModel>();
        services.AddTransient<PaymentDialogViewModel>();
        services.AddTransient<SellPackageDialogViewModel>();
        return services;
    }
}
