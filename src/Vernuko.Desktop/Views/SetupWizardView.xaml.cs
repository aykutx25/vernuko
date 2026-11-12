using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Vernuko.Presentation;

namespace Vernuko.Desktop.Views;

/// <summary>Shows exactly one step panel (named after the <see cref="SetupStep"/> value).</summary>
public partial class SetupWizardView : UserControl
{
    public SetupWizardView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged old)
            {
                old.PropertyChanged -= OnVmChanged;
            }

            if (e.NewValue is INotifyPropertyChanged vm)
            {
                vm.PropertyChanged += OnVmChanged;
            }

            UpdateStep();
        };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SetupWizardViewModel.Step))
        {
            UpdateStep();
        }
    }

    private void UpdateStep()
    {
        if (DataContext is not SetupWizardViewModel vm)
        {
            return;
        }

        foreach (var step in Enum.GetValues<SetupStep>())
        {
            if (FindName(step.ToString()) is UIElement panel)
            {
                panel.Visibility = step == vm.Step ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
