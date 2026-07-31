using Skua.Core.Interfaces;
using Skua.Core.Models;
using Velopack;
using Velopack.Sources;

namespace Skua.Core.Services;

public class ClientUpdateService : IClientUpdateService
{
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private UpdateManager? _updateManager;
    private Velopack.UpdateInfo? _pendingUpdate;
    private bool? _managerNightly;

    public ClientUpdateService(IDialogService dialogService, ISettingsService settingsService)
    {
        _dialogService = dialogService;
        _settingsService = settingsService;
    }

    public string? LatestVersion => _pendingUpdate?.TargetFullRelease.Version.ToString();

    public bool UpdateAvailable => _pendingUpdate is not null;

    public bool UsingNightlyChannel => _settingsService.Get("UseNightlyBuilds", false);

    public async Task CheckForUpdateAsync()
    {
        try
        {
            _updateManager = GetUpdateManager();
            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
        }
        catch (Exception e)
        {
            _pendingUpdate = null;
            _dialogService.ShowMessageBox($"Error Message:\r\n{e.Message}", "Update Error");
            throw;
        }
    }

    public async Task DownloadUpdateAsync(IProgress<string>? progress)
    {
        try
        {
            _updateManager = GetUpdateManager();
            _pendingUpdate ??= await _updateManager.CheckForUpdatesAsync();

            if (_pendingUpdate is null)
            {
                progress?.Report("You have the latest version.");
                return;
            }

            progress?.Report($"Downloading {_pendingUpdate.TargetFullRelease.Version}...");
            await _updateManager.DownloadUpdatesAsync(
                _pendingUpdate,
                value => progress?.Report($"Downloading... {value}%"));

            progress?.Report("Installing update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch (Exception e)
        {
            progress?.Report("Error while updating.");
            _dialogService.ShowMessageBox($"Error Message:\r\n{e.Message}", "Update Error");
        }
    }

    private UpdateManager GetUpdateManager()
    {
        bool nightly = UsingNightlyChannel;
        if (_updateManager is not null && _managerNightly == nightly)
            return _updateManager;

        _managerNightly = nightly;
        _pendingUpdate = null;

        if (!nightly)
            return new UpdateManager(
                new GithubSource("https://github.com/auqw/Skua", null, true),
                new UpdateOptions
                {
                    ExplicitChannel = "win",
                    AllowVersionDowngrade = ClientFileSources.IsNightly
                });

        return new UpdateManager(
            new GithubSource("https://github.com/auqw/Skua", null, true),
            new UpdateOptions
            {
                ExplicitChannel = "nightly",
                AllowVersionDowngrade = true
            });
    }
}
