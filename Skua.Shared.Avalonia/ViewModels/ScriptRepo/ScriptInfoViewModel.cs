using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Messaging;
using Skua.Core.Models;
using Skua.Core.Models.GitHub;
using System.Collections.ObjectModel;

namespace Skua.Shared.Avalonia.ViewModels.ScriptRepo;

public partial class ScriptInfoViewModel : ObservableObject
{
    public ScriptInfoViewModel(ScriptInfo info, string? localFileOverride = null, bool isCustom = false)
    {
        Info = info;
        _localFileOverride = localFileOverride;
        IsCustom = isCustom;
        _downloaded = DownloadedFileExists;
    }

    public ScriptInfo Info { get; }
    private readonly string? _localFileOverride;

    public string FileName => Info.Name;
    public int Size => Info.Size;
    public string LocalFile => _localFileOverride ?? Info.LocalFile;
    public string FilePath => Info.FilePath;
    public bool IsCustom { get; }
    public string DisplayPath => IsCustom ? LocalFile : ScriptPath;

    private bool DownloadedFileExists => File.Exists(LocalFile);
    public string ScriptPath => Path.Combine(ClientFileSources.SkuaScriptsDIR, FilePath.Replace("/", "\\"));

    private ObservableCollection<string>? _infoTags;
    public ObservableCollection<string> InfoTags => _infoTags ??= new(Info.Tags);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Outdated))]
    private bool _downloaded;

    public bool Outdated => !IsCustom && Downloaded && Info.LocalSize != Info.Size;

    [RelayCommand]
    private void LoadScript(ScriptInfoViewModel selectedScript)
    {
        if (selectedScript is null || !selectedScript.Downloaded)
            return;

        StrongReferenceMessenger.Default.Send<LoadScriptMessage, int>(new(selectedScript.LocalFile), (int)MessageChannels.ScriptStatus);
    }

    [RelayCommand]
    private void StartScript(ScriptInfoViewModel selectedScript)
    {
        if (selectedScript is null || !selectedScript.Downloaded)
            return;

        StrongReferenceMessenger.Default.Send<StartScriptMessage, int>(new(selectedScript.LocalFile), (int)MessageChannels.ScriptStatus);
    }

    public override string ToString()
    {
        return FileName;
    }
}
