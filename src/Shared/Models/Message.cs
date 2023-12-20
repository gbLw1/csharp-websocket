using System.Text.Json.Serialization;

namespace Shared.Models;

public class Message
{
    [JsonPropertyName("from")]
    public required string From { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [JsonPropertyName("room")]
    public required string Room { get; set; }
}
