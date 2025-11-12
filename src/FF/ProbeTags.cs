using System.Text.Json.Serialization;

namespace TVideo.FF;

public class ProbeTags
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
