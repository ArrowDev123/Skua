using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Utils;
using Skua.Shared.Avalonia.ViewModels;
using Skua.Shared.Avalonia.ViewModels.Options;
using System;
using System.Collections.Generic;

namespace Skua.Manager.Avalonia.ViewModels;

public class ManagerOptionsViewModel : ObservableObject
{
    public ManagerOptionsViewModel(List<DisplayOptionItemViewModelBase> options, List<DisplayOptionItemViewModelBase> devOptions, ISettingsService settingsService)
    {
        ManagerOptions = options;
        DevOptions = devOptions;
        _settingsService = settingsService;
        OpenGHAuthCommand = new RelayCommand(OpenGHAuthDialog);
    }

    public List<DisplayOptionItemViewModelBase> ManagerOptions { get; }
    public List<DisplayOptionItemViewModelBase> DevOptions { get; }

    private readonly ISettingsService _settingsService;

    public IRelayCommand OpenGHAuthCommand { get; }

    private void OpenGHAuthDialog()
    {
        string? previousToken = _settingsService.Get<string>("UserGitHubToken");
        Ioc.Default.GetRequiredService<IDialogService>().ShowDialog(Ioc.Default.GetRequiredService<GitHubAuthViewModel>());

        string? token = _settingsService.Get<string>("UserGitHubToken");
        if (!string.IsNullOrWhiteSpace(token) && token != previousToken)
            HttpClients.UserGitHubClient = new(token);
    }
}
