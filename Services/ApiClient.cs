using Microsoft.Extensions.Logging;
using Research_Arcade_Updater.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Research_Arcade_Updater.Services
{
    public interface IApiClient
    {
        Task<LauncherInfo> GetLauncherInfoAsync(ILogger<UpdaterService> _logger);
        Task<bool> UpdateRemoteLauncherVersionAsync(string newVersion, ILogger<UpdaterService> _logger);
    }
    public class ApiClient(HttpClient http) : IApiClient
    {
        private readonly HttpClient _http = http;

        public async Task<LauncherInfo> GetLauncherInfoAsync(ILogger<UpdaterService> _logger)
        {
            var response = await _http.GetAsync("/api/LauncherVersions/Latest");

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    _logger.LogWarning("Warning: {message}", errorMessage);

                else
                    _logger.LogError("Unexpected error: {StatusCode}", response.StatusCode);

                throw new InvalidOperationException("Failed to retrieve LauncherInfo.");
            }
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var updaterInfo = await JsonSerializer.DeserializeAsync<LauncherInfo>(stream, options);
            return updaterInfo ?? throw new InvalidOperationException("Failed to deserialize LauncherInfo.");
        }

        public async Task<bool> UpdateRemoteLauncherVersionAsync(string newVersion, ILogger<UpdaterService> _logger)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { VersionNumber = newVersion }),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            var response = await _http.PutAsync("/api/MachineLauncherAssignments/UpdateVersion", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    _logger.LogWarning("Bad Request: {message}", errorMessage);

                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    _logger.LogWarning("Not Found: {message}", errorMessage);

                else
                    _logger.LogError("Unexpected error: {StatusCode}", response.StatusCode);
            }
            response.EnsureSuccessStatusCode();

            return response.IsSuccessStatusCode;
        }
    }
}
