using System.Text.Json.Serialization;

namespace TVideo.FF;

public class StreamTags
{
    [JsonPropertyName("language")]
    public required string Language { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    public override string ToString()
    {
        return $"{Language} {Title}";
    }
}
