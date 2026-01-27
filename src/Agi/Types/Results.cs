using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agi.Types;

/// <summary>
/// Result from a completed task
/// </summary>
public class TaskResult
{
    /// <summary>
    /// Task output data
    /// </summary>
    public JsonElement? Data { get; set; }

    /// <summary>
    /// Task metadata
    /// </summary>
    public TaskMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Get data as a specific type
    /// </summary>
    public T? GetData<T>()
    {
        if (Data == null)
            return default;
        return JsonSerializer.Deserialize<T>(Data.Value.GetRawText());
    }
}

/// <summary>
/// Metadata about a completed task
/// </summary>
public class TaskMetadata
{
    /// <summary>
    /// Task ID
    /// </summary>
    public int TaskId { get; set; }

    /// <summary>
    /// Session ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Cost in credits
    /// </summary>
    public double Cost { get; set; }

    /// <summary>
    /// Completion timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Number of steps taken
    /// </summary>
    public int Steps { get; set; }

    /// <summary>
    /// Whether the task succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// All messages from the session
    /// </summary>
    public List<MessageResponse> Messages { get; set; } = new();
}

/// <summary>
/// Screenshot data from the browser
/// </summary>
public class Screenshot
{
    /// <summary>
    /// Base64-encoded screenshot data
    /// </summary>
    [JsonPropertyName("screenshot")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Current page URL
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Page title
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Screenshot width in pixels
    /// </summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    /// <summary>
    /// Screenshot height in pixels
    /// </summary>
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    /// <summary>
    /// Get screenshot as byte array
    /// </summary>
    public byte[] GetBytes()
    {
        return Convert.FromBase64String(Data);
    }

    /// <summary>
    /// Save screenshot to file
    /// </summary>
    public async Task SaveToFileAsync(string path)
    {
        var bytes = GetBytes();
        await File.WriteAllBytesAsync(path, bytes);
    }
}
