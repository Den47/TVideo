using System.Text.Json.Serialization;

namespace TVideo.FF;

public class StreamInfo
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("codec_name")]
    public required string CodecName { get; set; }

    [JsonPropertyName("codec_type")]
    public required string CodecType { get; set; }

    [JsonPropertyName("channels")]
    public int Channels { get; set; }

    [JsonPropertyName("channel_layout")]
    public required string ChannelLayout { get; set; }

    [JsonPropertyName("tags")]
    public required StreamTags Tags { get; set; }
}
