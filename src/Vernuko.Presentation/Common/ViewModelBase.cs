using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Vernuko.Core;

namespace Vernuko.Presentation.Common;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _infoMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasInfo => !string.IsNullOrEmpty(InfoMessage);

    protected static ILogger? Logger { get; private set; }

    public static void UseLogger(ILogger logger) => Logger = logger;

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnInfoMessageChanged(string? value) => OnPropertyChanged(nameof(HasInfo));

    /// <summary>Runs an action with busy state; unexpected exceptions become a friendly message (details go to the log).</summary>
    protected async Task RunAsync(Func<Task> action, bool clearMessages = true)
    {
        if (clearMessages)
        {
            ErrorMessage = null;
            InfoMessage = null;
        }

        IsBusy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unhandled error in {ViewModel}", GetType().Name);
            ErrorMessage = "Beklenmeyen bir hata oluştu. İşlem tamamlanmadı; ayrıntılar günlük dosyasına yazıldı.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Shows a failed result's message and returns false.</summary>
    protected bool Check(Result result)
    {
        if (result.IsFailure)
        {
            ErrorMessage = result.Error.Message;
            return false;
        }

        return true;
    }
}

/// <summary>A page in the main navigation.</summary>
public abstract class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }

    /// <summary>Called every time the page is shown.</summary>
    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;
}
