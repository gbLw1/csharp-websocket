using Shared.Enums;
using System.Text.Json.Serialization;

namespace Shared.Models;

public class Message
{
    /// <summary>
    /// <para>Required</para>
    /// <para>The type of the message</para>
    /// <para>See the values of <see cref="MessageType"/>.</para>
    /// </summary>
    [JsonPropertyName("type")]
    public required MessageType Type { get; set; }

    /// <summary>
    /// <para>The content of the message.</para>
    /// <para>Required for <see cref="MessageType.Message"/>.</para>
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; set; }

    /// <summary>
    /// <para>Automagically set by the server (ignored if client sets it).</para>
    /// <para>The nickname of the sender.</para>
    /// </summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>
    /// <para>Automagically set by the server (ignored if client sets it).</para>
    /// <para>Sender's room.</para>
    /// </summary>
    [JsonPropertyName("to")]
    public string? To { get; set; }

    /// <summary>
    /// <para>Notifies the client that the sender is typing.</para>
    /// <para>Required for <see cref="MessageType.Notification"/>.</para>
    /// </summary>
    [JsonPropertyName("isTyping")]
    public bool? IsTyping { get; set; }
}
