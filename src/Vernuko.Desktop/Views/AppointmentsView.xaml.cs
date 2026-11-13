using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Vernuko.Presentation.Pages;

namespace Vernuko.Desktop.Views;

public partial class AppointmentsView : UserControl
{
    public AppointmentsView()
    {
        InitializeComponent();
    }

    /// <summary>Builds the per-row action menu from the row's allowed status transitions.</summary>
    private void OnRowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AppointmentRow row } element || DataContext is not AppointmentsViewModel vm)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = element, Placement = PlacementMode.Bottom };
        foreach (var action in row.Actions)
        {
            menu.Items.Add(new MenuItem { Header = action.Label, Command = vm.ChangeStatusCommand, CommandParameter = new object[] { row, action } });
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        if (row.CanReschedule && vm.CanManage)
        {
            menu.Items.Add(new MenuItem { Header = "Yeni saate taşı…", Command = vm.RescheduleCommand, CommandParameter = row });
        }

        if (row.CanCancelSeries)
        {
            menu.Items.Add(new MenuItem { Header = "Seriyi bu randevudan itibaren iptal et…", Command = vm.CancelSeriesCommand, CommandParameter = row });
        }

        menu.Items.Add(new MenuItem { Header = "WhatsApp ile hatırlat", Command = vm.SendWhatsAppCommand, CommandParameter = row });
        menu.Items.Add(new MenuItem { Header = "Ödeme al", Command = vm.TakePaymentCommand, CommandParameter = row });
        menu.Items.Add(new MenuItem { Header = "Müşteri kartını aç", Command = vm.OpenCustomerCommand, CommandParameter = row });
        menu.IsOpen = true;
    }
}
