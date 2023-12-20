using System.Text.Json.Serialization;

namespace Shared.Models;

public class Client
{
    /// <summary>
    /// <para>Automatically generated</para>
    /// <para>The unique identifier of the client.</para>
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id => Guid.NewGuid();

    /// <summary>
    /// <para>Required</para>
    /// <para>The nickname of the client.</para>
    /// </summary>
    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }

    /// <summary>
    /// <para>Automatically generated</para>
    /// <para>The color of the client's nickname appearing in the chat.</para>
    /// </summary>
    [JsonPropertyName("color")]
    public static string Color => GenerateRandomHexColor();

    /// <summary>
    /// <para>Required</para>
    /// <para>The room the client is in, used to send messages to the correct room.</para>
    /// </summary>
    [JsonPropertyName("room")]
    public required string Room { get; init; }

    private static string GenerateRandomHexColor()
    {
        var random = new Random();
        var color = string.Format("#{0:X6}", random.Next(0x1000000));

        if (color.Length != 7)
        {
            color = color.PadLeft(7, '0');
        }

        return color;
    }
}