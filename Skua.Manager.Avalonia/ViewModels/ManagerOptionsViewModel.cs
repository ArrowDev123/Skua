using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Interfaces.Services;
using Skua.Core.Models;
using Skua.Core.Utils;
using Skua.Shared.Avalonia.ViewModels;
using Skua.Shared.Avalonia.ViewModels.Options;
using System;
using System.Collections.Generic;

namespace Skua.Manager.Avalonia.ViewModels;

public class ManagerOptionsViewModel : ObservableObject
{
    public ManagerOptionsViewModel(
        List<DisplayOptionItemViewModelBase> options,
        List<DisplayOptionItemViewModelBase> devOptions,
        ISettingsService settingsService,
        ICustomScriptService customScripts,
        IFileDialogService fileDialogService)
    {
        ManagerOptions = options;
        DevOptions = devOptions;
        _settingsService = settingsService;
        _customScripts = customScripts;
        _fileDialogService = fileDialogService;
        OpenGHAuthCommand = new RelayCommand(OpenGHAuthDialog);
        SelectCustomScriptsFolderCommand = new RelayCommand(SelectCustomScriptsFolder);
        ClearCustomScriptsFolderCommand = new RelayCommand(ClearCustomScriptsFolder);
    }

    public List<DisplayOptionItemViewModelBase> ManagerOptions { get; }
    public List<DisplayOptionItemViewModelBase> DevOptions { get; }

    private readonly ISettingsService _settingsService;
    private readonly ICustomScriptService _customScripts;
    private readonly IFileDialogService _fileDialogService;

    public IRelayCommand OpenGHAuthCommand { get; }
    public IRelayCommand SelectCustomScriptsFolderCommand { get; }
    public IRelayCommand ClearCustomScriptsFolderCommand { get; }
    public string CustomScriptsFolder
    {
        get
        {
            IReadOnlyList<string> roots = _customScripts.GetSearchRoots();
            return roots.Count == 0 ? "Not configured" : roots[0];
        }
    }

    private void SelectCustomScriptsFolder()
    {
        string initialDirectory = CustomScriptsFolder == "Not configured"
            ? ClientFileSources.SkuaDIR
            : CustomScriptsFolder;
        string? folder = _fileDialogService.OpenFolder(initialDirectory);
        if (string.IsNullOrWhiteSpace(folder))
            return;

        _customScripts.SetCustomFolder(folder);
        OnPropertyChanged(nameof(CustomScriptsFolder));
    }

    private void ClearCustomScriptsFolder()
    {
        _customScripts.SetCustomFolder(null);
        OnPropertyChanged(nameof(CustomScriptsFolder));
    }

    private void OpenGHAuthDialog()
    {
        string? previousToken = _settingsService.Get<string>("UserGitHubToken");
        Ioc.Default.GetRequiredService<IDialogService>().ShowDialog(Ioc.Default.GetRequiredService<GitHubAuthViewModel>());

        string? token = _settingsService.Get<string>("UserGitHubToken");
        if (!string.IsNullOrWhiteSpace(token) && token != previousToken)
            HttpClients.UserGitHubClient = new(token);
    }
}
