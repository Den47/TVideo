using System.Text.Json.Serialization;

namespace TVideo.FF;

public class ProbeResult
{
    [JsonPropertyName("streams")]
    public List<ProbeStream> Streams { get; set; } = [];


    [JsonPropertyName("format")]
    public ProbeFormat? Format { get; set; }
}
