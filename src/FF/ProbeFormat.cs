using System.Text.Json.Serialization;

namespace TVideo.FF;

public class ProbeFormat
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}
