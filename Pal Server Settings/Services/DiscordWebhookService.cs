using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PalWorldServerManager.Services
{
    public sealed class DiscordWebhookService : IDisposable
    {
        private readonly HttpClient _httpClient =
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(10)
            };

        public async Task SendAsync(
            string webhookUrl,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(
                    webhookUrl))
            {
                throw new ArgumentException(
                    "A Discord webhook URL is required.",
                    nameof(webhookUrl));
            }

            if (!Uri.TryCreate(
                    webhookUrl,
                    UriKind.Absolute,
                    out Uri? uri) ||
                !uri.Scheme.Equals(
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The Discord webhook URL is invalid.");
            }

            if (string.IsNullOrWhiteSpace(
                    message))
            {
                throw new ArgumentException(
                    "A notification message is required.",
                    nameof(message));
            }

            string json =
                JsonSerializer.Serialize(
                    new
                    {
                        content = message
                    });

            using StringContent content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            using HttpResponseMessage response =
                await _httpClient.PostAsync(
                    uri,
                    content,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string responseText =
                    await response.Content.ReadAsStringAsync(
                        cancellationToken);

                throw new HttpRequestException(
                    $"Discord returned HTTP {(int)response.StatusCode}. {responseText}");
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}