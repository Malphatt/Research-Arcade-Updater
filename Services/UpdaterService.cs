using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Research_Arcade_Updater.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Research_Arcade_Updater.Services
{
    public interface IUpdaterService
    {
        event EventHandler<LauncherStateChangedEventArgs> StateChanged;

        Task CheckAndUpdateAsync(CancellationToken cancellationToken);
        Task ResetRemoteMachineLauncherVersionAsync(CancellationToken cancellationToken);
    }

    public class LauncherStateChangedEventArgs(UpdaterState newState) : EventArgs
    {
        public UpdaterState NewState { get; } = newState;
    }

    public class UpdaterService : IUpdaterService
    {
        public event EventHandler<LauncherStateChangedEventArgs> StateChanged;
        protected void OnStateChanged(UpdaterState s) =>
            StateChanged?.Invoke(this, new LauncherStateChangedEventArgs(s));

        private readonly IApiClient _apiClient;
        private readonly ILogger<UpdaterService> _logger;
        private readonly string _rootPath;
        private readonly string _launcherDir;

        public UpdaterService(
            IApiClient apiClient,
            ILogger<UpdaterService> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
            _rootPath = Directory.GetCurrentDirectory();
            _launcherDir = Path.Combine(_rootPath, "Launcher");
        }

        public async Task ResetRemoteMachineLauncherVersionAsync(CancellationToken cancellationToken)
        {
            OnStateChanged(UpdaterState.failed);

            try
            {
                _logger.LogInformation("Resetting remote machine launcher version...");
                bool result = await _apiClient.UpdateRemoteLauncherVersionAsync("0.0.0", _logger);

                if (result)
                    _logger.LogInformation("Successfully reset remote machine launcher version.");
                else
                    _logger.LogWarning("Failed to reset remote machine launcher version.");

                OnStateChanged(UpdaterState.idle);
            }
            catch (Exception)
            {
                _logger.LogError("Error resetting remote machine launcher version.");
                OnStateChanged(UpdaterState.failed);
            }
        }

        public async Task CheckAndUpdateAsync(CancellationToken cancellationToken)
        {
            OnStateChanged(UpdaterState.checkingForUpdates);
            try
            {
                _logger.LogInformation("Checking for updates...");
                var info = await _apiClient.GetLauncherInfoAsync(_logger);

                OnStateChanged(UpdaterState.updatingLauncher);

                await DownloadAndExtractAsync(info, cancellationToken);

                try
                {
                    bool updateResult = await _apiClient.UpdateRemoteLauncherVersionAsync(info.VersionNumber, _logger);

                    if (updateResult)
                        _logger.LogInformation("Successfully updated launcher version to {VersionNumber}.", info.VersionNumber);
                    else
                        _logger.LogWarning("Failed to update launcher version to {VersionNumber}.", info.VersionNumber);
                }
                catch (Exception)
                {
                    OnStateChanged(UpdaterState.failed);
                }
            }
            catch (Exception)
            {
                OnStateChanged(UpdaterState.failed);
            }
        }

        private async Task DownloadAndExtractAsync(LauncherInfo info, CancellationToken cancellationToken)
        {
            OnStateChanged(UpdaterState.updatingLauncher);

            // Delete the old launcher files (except the Games folder)
            foreach (string file in Directory.GetFiles(_launcherDir))
                if (Path.GetFileName(file) != "Games")
                    File.Delete(file);

            // Download the launcher using HttpClient
            using HttpClient httpClient = new();
            var response = await httpClient.GetAsync(info.FileUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var zipFilePath = Path.Combine(_launcherDir, "Launcher.zip");
            await using (var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            // Extract the zip file
            FastZip fastZip = new();
            fastZip.ExtractZip(zipFilePath, _launcherDir, null);

            // Delete the zip file
            File.Delete(zipFilePath);
        }
    }
}
