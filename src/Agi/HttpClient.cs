using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Agi.Types;

namespace Agi;

/// <summary>
/// Internal HTTP client for AGI API requests
/// </summary>
internal class AgiHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly int _maxRetries;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes = new()
    {
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    };

    public AgiHttpClient(string apiKey, string baseUrl, TimeSpan timeout, int maxRetries)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _maxRetries = maxRetries;

        _httpClient = new HttpClient
        {
            Timeout = timeout
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("agi-csharp/1.0.0");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Make a request to the API
    /// </summary>
    public async Task<T> RequestAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        Dictionary<string, string>? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_baseUrl + path, query);
        return await RequestUrlAsync<T>(method, url, body, cancellationToken);
    }

    /// <summary>
    /// Make a request to an absolute URL
    /// </summary>
    public async Task<T> RequestUrlAsync<T>(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _maxRetries)
        {
            try
            {
                using var request = new HttpRequestMessage(method, url);

                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body, _jsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (ShouldRetry(response.StatusCode) && attempt < _maxRetries)
                    {
                        var delay = GetRetryDelay(attempt, response);
                        await Task.Delay(delay, cancellationToken);
                        attempt++;
                        continue;
                    }

                    throw CreateException(response.StatusCode, content, response.Headers);
                }

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)content;
                }

                var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                if (result == null)
                {
                    throw new AgiException("Failed to deserialize response");
                }

                return result;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is System.TimeoutException)
            {
                throw new Agi.TimeoutException("Request timed out", _httpClient.Timeout);
            }
            catch (HttpRequestException ex)
            {
                lastException = new ConnectionException($"Connection error: {ex.Message}", ex);

                if (attempt < _maxRetries)
                {
                    var delay = GetRetryDelay(attempt, null);
                    await Task.Delay(delay, cancellationToken);
                    attempt++;
                    continue;
                }

                throw lastException;
            }
        }

        throw lastException ?? new AgiException("Request failed after retries");
    }

    /// <summary>
    /// Stream server-sent events from the API
    /// </summary>
    public async IAsyncEnumerable<SSEEvent> StreamEventsAsync(
        string path,
        Dictionary<string, string>? query = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_baseUrl + path, query);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateException(response.StatusCode, content, response.Headers);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? eventId = null;
        string? eventType = null;
        var dataLines = new List<string>();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
#if NET7_0_OR_GREATER
            var line = await reader.ReadLineAsync(cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
#endif

            if (line == null)
                break;

            if (string.IsNullOrEmpty(line))
            {
                // Empty line = dispatch event
                if (eventType != null && dataLines.Count > 0)
                {
                    var data = string.Join("\n", dataLines);
                    var evt = ParseEvent(eventId, eventType, data);
                    if (evt != null)
                    {
                        yield return evt;

                        if (evt.Event == EventType.Done || evt.Event == EventType.Error)
                        {
                            yield break;
                        }
                    }
                }

                eventId = null;
                eventType = null;
                dataLines.Clear();
                continue;
            }

            if (line.StartsWith("id:"))
            {
                eventId = line[3..].Trim();
            }
            else if (line.StartsWith("event:"))
            {
                eventType = line[6..].Trim();
            }
            else if (line.StartsWith("data:"))
            {
                dataLines.Add(line[5..].TrimStart());
            }
        }
    }

    private SSEEvent? ParseEvent(string? id, string eventType, string data)
    {
        if (!Enum.TryParse<EventType>(eventType, true, out var type))
        {
            // Unknown event type, skip
            return null;
        }

        JsonElement dataElement;
        try
        {
            dataElement = JsonSerializer.Deserialize<JsonElement>(data, _jsonOptions);
        }
        catch
        {
            // If data isn't valid JSON, wrap it as a string
            dataElement = JsonSerializer.Deserialize<JsonElement>($"\"{data}\"");
        }

        return new SSEEvent
        {
            Id = id,
            Event = type,
            Data = dataElement
        };
    }

    private static string BuildUrl(string baseUrl, Dictionary<string, string>? query)
    {
        if (query == null || query.Count == 0)
            return baseUrl;

        var queryString = string.Join("&",
            query.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{baseUrl}?{queryString}";
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return RetryableStatusCodes.Contains(statusCode);
    }

    private static TimeSpan GetRetryDelay(int attempt, HttpResponseMessage? response)
    {
        // Check for Retry-After header
        if (response?.Headers.RetryAfter?.Delta != null)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }

        // Exponential backoff: 1s, 2s, 4s, 8s...
        var delay = Math.Pow(2, attempt);
        // Add jitter (0-500ms)
        var jitter = Random.Shared.NextDouble() * 0.5;
        return TimeSpan.FromSeconds(delay + jitter);
    }

    private static AgiException CreateException(HttpStatusCode statusCode, string content, HttpResponseHeaders headers)
    {
        var message = TryExtractErrorMessage(content) ?? $"Request failed with status {(int)statusCode}";

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new AuthenticationException(message, content),
            HttpStatusCode.Forbidden => new PermissionException(message, content),
            HttpStatusCode.NotFound => new NotFoundException(message, content),
            HttpStatusCode.TooManyRequests => new RateLimitException(
                message,
                content,
                headers.RetryAfter?.Delta),
            HttpStatusCode.UnprocessableEntity => CreateValidationException(message, content),
            >= HttpStatusCode.InternalServerError => new ApiException(message, (int)statusCode, content),
            _ => new AgiException(message, (int)statusCode, content)
        };
    }

    private static ValidationException CreateValidationException(string message, string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("errors", out var errorsElement) ||
                doc.RootElement.TryGetProperty("detail", out errorsElement))
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var prop in errorsElement.EnumerateObject())
                {
                    var fieldErrors = prop.Value.ValueKind == JsonValueKind.Array
                        ? prop.Value.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                        : new[] { prop.Value.GetString() ?? "" };
                    errors[prop.Name] = fieldErrors;
                }
                return new ValidationException(message, errors, content);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return new ValidationException(message);
    }

    private static string? TryExtractErrorMessage(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();
                if (error.TryGetProperty("message", out var msg))
                    return msg.GetString();
            }
            if (doc.RootElement.TryGetProperty("message", out var message))
                return message.GetString();
            if (doc.RootElement.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String)
                return detail.GetString();
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
