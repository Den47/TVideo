using System.Text.Json.Serialization;

namespace TVideo.FF;

public class ProbeStream
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("codec_name")]
    public string? CodecName { get; set; }

    [JsonPropertyName("codec_type")]
    public string? CodecType { get; set; }

    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("tags")]
    public ProbeTags? Tags { get; set; }
}
