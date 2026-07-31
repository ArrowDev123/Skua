using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;

namespace Skua.App.Avalonia.ViewModels.MainMenu;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Skua";

    public MainViewModel()
    {
        _title = $"Skua - {ClientFileSources.DisplayVersion}";
    }

    [RelayCommand]
    private void ShowMainWindow()
    {
        StrongReferenceMessenger.Default.Send<ShowMainWindowMessage>();
    }
}
