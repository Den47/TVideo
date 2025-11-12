using System.Text.Json.Serialization;

namespace TVideo.FF;

public class FFProbeResult
{
    [JsonPropertyName("streams")]
    public required List<StreamInfo> Streams { get; set; }
}
