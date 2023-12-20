using System.Text.Json.Serialization;

namespace Shared.Models;

public class Client
{
    public Client()
    {
        Id = Guid.NewGuid();
        Color = GenerateRandomHexColor();
    }

    /// <summary>
    /// <para>Automatically generated</para>
    /// <para>The unique identifier of the client.</para>
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// <para>Required</para>
    /// <para>The nickname of the client.</para>
    /// </summary>
    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }

    /// <summary>
    /// <para>Automatically generated</para>
    /// <para>The avatar of the client.</para>
    /// </summary>
    [JsonPropertyName("avatar")]
    public string Avatar => "https://avatars.githubusercontent.com/u/17113905?v=4";

    /// <summary>
    /// <para>Automatically generated</para>
    /// <para>The color of the client's nickname appearing in the chat.</para>
    /// </summary>
    [JsonPropertyName("color")]
    public string Color { get; init; }

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