using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PalWorldServerManager.Services
{
    public sealed class PalworldRestApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public PalworldRestApiService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<bool> TestConnectionAsync(
            int port,
            string adminPassword,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await GetServerInfoAsync(
                    port,
                    adminPassword,
                    cancellationToken);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<PalworldServerInfo> GetServerInfoAsync(
            int port,
            string adminPassword,
            CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage request =
                CreateRequest(
                    HttpMethod.Get,
                    port,
                    "info",
                    adminPassword);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "read server information",
                cancellationToken);

            string json =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            PalworldServerInfo? information =
                JsonSerializer.Deserialize<PalworldServerInfo>(
                    json,
                    _jsonOptions);

            return information
                ?? throw new InvalidOperationException(
                    "The server returned an empty information response.");
        }

        public async Task<PalworldPlayersResponse> GetPlayersAsync(
            int port,
            string adminPassword,
            CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage request =
                CreateRequest(
                    HttpMethod.Get,
                    port,
                    "players",
                    adminPassword);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "read the player list",
                cancellationToken);

            string json =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            PalworldPlayersResponse? players =
                JsonSerializer.Deserialize<PalworldPlayersResponse>(
                    json,
                    _jsonOptions);

            return players
                ?? new PalworldPlayersResponse();
        }

        public async Task<PalworldServerMetrics> GetMetricsAsync(
            int port,
            string adminPassword,
            CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage request =
                CreateRequest(
                    HttpMethod.Get,
                    port,
                    "metrics",
                    adminPassword);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "read server metrics",
                cancellationToken);

            string json =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            PalworldServerMetrics? metrics =
                JsonSerializer.Deserialize<PalworldServerMetrics>(
                    json,
                    _jsonOptions);

            return metrics
                ?? throw new InvalidOperationException(
                    "The server returned an empty metrics response.");
        }

        public async Task KickPlayerAsync(
            int port,
            string adminPassword,
            string userId,
            string message,
            CancellationToken cancellationToken = default)
        {
            ValidateUserId(userId);

            PlayerModerationRequest body =
                new PlayerModerationRequest
                {
                    UserId = userId.Trim(),
                    Message = message ?? ""
                };

            using HttpRequestMessage request =
                CreateJsonRequest(
                    HttpMethod.Post,
                    port,
                    "kick",
                    adminPassword,
                    body);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "kick the player",
                cancellationToken);
        }

        public async Task BanPlayerAsync(
            int port,
            string adminPassword,
            string userId,
            string message,
            CancellationToken cancellationToken = default)
        {
            ValidateUserId(userId);

            PlayerModerationRequest body =
                new PlayerModerationRequest
                {
                    UserId = userId.Trim(),
                    Message = message ?? ""
                };

            using HttpRequestMessage request =
                CreateJsonRequest(
                    HttpMethod.Post,
                    port,
                    "ban",
                    adminPassword,
                    body);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "ban the player",
                cancellationToken);
        }

        public async Task UnbanPlayerAsync(
            int port,
            string adminPassword,
            string userId,
            CancellationToken cancellationToken = default)
        {
            ValidateUserId(userId);

            UnbanPlayerRequest body =
                new UnbanPlayerRequest
                {
                    UserId = userId.Trim()
                };

            using HttpRequestMessage request =
                CreateJsonRequest(
                    HttpMethod.Post,
                    port,
                    "unban",
                    adminPassword,
                    body);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "unban the player",
                cancellationToken);
        }

        public async Task SaveWorldAsync(
            int port,
            string adminPassword,
            CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage request =
                CreateRequest(
                    HttpMethod.Post,
                    port,
                    "save",
                    adminPassword);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "save the world",
                cancellationToken);
        }

        public async Task ShutdownAsync(
            int port,
            string adminPassword,
            int waitTimeSeconds,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (waitTimeSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(waitTimeSeconds),
                    "The shutdown delay cannot be negative.");
            }

            ShutdownRequest body = new ShutdownRequest
            {
                WaitTime = waitTimeSeconds,
                Message = message ?? ""
            };

            using HttpRequestMessage request =
                CreateJsonRequest(
                    HttpMethod.Post,
                    port,
                    "shutdown",
                    adminPassword,
                    body);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "shut down the server",
                cancellationToken);
        }

        public async Task ForceStopAsync(
            int port,
            string adminPassword,
            CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage request =
                CreateRequest(
                    HttpMethod.Post,
                    port,
                    "stop",
                    adminPassword);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "force stop the server",
                cancellationToken);
        }

        public async Task AnnounceAsync(
            int port,
            string adminPassword,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "An announcement message is required.",
                    nameof(message));
            }

            AnnouncementRequest body = new AnnouncementRequest
            {
                Message = message.Trim()
            };

            using HttpRequestMessage request =
                CreateJsonRequest(
                    HttpMethod.Post,
                    port,
                    "announce",
                    adminPassword,
                    body);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            await EnsureSuccessAsync(
                response,
                "send the announcement",
                cancellationToken);
        }

        private HttpRequestMessage CreateRequest(
            HttpMethod method,
            int port,
            string endpoint,
            string adminPassword)
        {
            ValidateConnectionSettings(
                port,
                adminPassword);

            HttpRequestMessage request =
                new HttpRequestMessage(
                    method,
                    BuildEndpointUri(
                        port,
                        endpoint));

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

            request.Headers.Authorization =
                CreateBasicAuthenticationHeader(
                    adminPassword);

            return request;
        }

        private HttpRequestMessage CreateJsonRequest<T>(
            HttpMethod method,
            int port,
            string endpoint,
            string adminPassword,
            T body)
        {
            HttpRequestMessage request =
                CreateRequest(
                    method,
                    port,
                    endpoint,
                    adminPassword);

            string json =
                JsonSerializer.Serialize(
                    body,
                    _jsonOptions);

            request.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            return request;
        }

        private static Uri BuildEndpointUri(
            int port,
            string endpoint)
        {
            string cleanedEndpoint =
                endpoint.TrimStart('/');

            return new Uri(
                $"http://127.0.0.1:{port}/v1/api/{cleanedEndpoint}");
        }

        private static AuthenticationHeaderValue
            CreateBasicAuthenticationHeader(
                string adminPassword)
        {
            string credentials =
                $"admin:{adminPassword}";

            string encodedCredentials =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        credentials));

            return new AuthenticationHeaderValue(
                "Basic",
                encodedCredentials);
        }

        private static void ValidateUserId(
            string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "A player User ID is required.",
                    nameof(userId));
            }
        }

        private static void ValidateConnectionSettings(
            int port,
            string adminPassword)
        {
            if (port < 1 ||
                port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port),
                    "The REST API port must be between 1 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(
                    adminPassword))
            {
                throw new InvalidOperationException(
                    "An Admin Password is required to use the REST API.");
            }
        }

        private static async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            string operation,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string responseText =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (response.StatusCode ==
                HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException(
                    "The Palworld REST API rejected the credentials. " +
                    "Check the Admin Password and restart the server " +
                    "after enabling the REST API.");
            }

            if (response.StatusCode ==
                HttpStatusCode.BadRequest)
            {
                throw new InvalidOperationException(
                    $"Palworld could not {operation}. " +
                    $"The request was rejected. {responseText}");
            }

            throw new HttpRequestException(
                $"Palworld could not {operation}. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.ReasonPhrase}. {responseText}");
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    public sealed class PalworldServerInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("servername")]
        public string ServerName { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("worldguid")]
        public string WorldGuid { get; set; } = "";
    }

    public sealed class PalworldPlayersResponse
    {
        [JsonPropertyName("players")]
        public List<PalworldPlayer> Players { get; set; } = new();
    }

    public sealed class PalworldPlayer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = "";

        [JsonPropertyName("playerId")]
        public string PlayerId { get; set; } = "";

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("ip")]
        public string IpAddress { get; set; } = "";

        [JsonPropertyName("ping")]
        public double Ping { get; set; }

        [JsonPropertyName("location_x")]
        public double LocationX { get; set; }

        [JsonPropertyName("location_y")]
        public double LocationY { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("building_count")]
        public int BuildingCount { get; set; }

        public string PingDisplay =>
            $"{Ping:0.0} ms";
    }

    public sealed class PalworldServerMetrics
    {
        [JsonPropertyName("serverfps")]
        public int ServerFps { get; set; }

        [JsonPropertyName("currentplayernum")]
        public int CurrentPlayerCount { get; set; }

        [JsonPropertyName("serverframetime")]
        public double ServerFrameTime { get; set; }

        [JsonPropertyName("maxplayernum")]
        public int MaximumPlayerCount { get; set; }

        [JsonPropertyName("uptime")]
        public long UptimeSeconds { get; set; }

        [JsonPropertyName("basecampnum")]
        public int BaseCampCount { get; set; }

        [JsonPropertyName("days")]
        public int WorldDays { get; set; }
    }

    internal sealed class UnbanPlayerRequest
    {
        [JsonPropertyName("userid")]
        public string UserId { get; set; } = "";
    }

    internal sealed class PlayerModerationRequest
    {
        [JsonPropertyName("userid")]
        public string UserId { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    internal sealed class ShutdownRequest
    {
        [JsonPropertyName("waittime")]
        public int WaitTime { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    internal sealed class AnnouncementRequest
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}