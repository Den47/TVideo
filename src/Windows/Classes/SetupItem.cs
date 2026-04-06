using TVideo.FF;

namespace TVideo.Windows.Classes;

public class SetupItem
{
    public SetupItem(ProbeStream info)
    {
        Info = info;
        Convert = IsAudio;
    }

    public ProbeStream Info { get; }

    public bool Convert { get; set; }

    public bool Remove { get; set; }

    public bool IsAudio => string.Equals(CodecType, "audio", StringComparison.OrdinalIgnoreCase);

    public bool IsVideo => string.Equals(CodecType, "video", StringComparison.OrdinalIgnoreCase);

    public bool IsSubtitle => string.Equals(CodecType, "subtitle", StringComparison.OrdinalIgnoreCase);

    public bool CanRemove => !IsVideo;

    public string NewCodec { get; } = "ac3";

    public string Language => Info.Tags?.Language?.Trim().ToUpper() ?? "[??]";

    private string? CodecType => Info.CodecType;

    public override string ToString()
    {
        var tags = Info.Tags;
        if (tags is null)
        {
            if (IsVideo)
                return "[Video NoTags]";

            if (IsAudio)
                return "[Audio NoTags]";

            if (IsSubtitle)
                return "[Subtitle NoTags]";

            return "[...]";
        }

        return string.IsNullOrWhiteSpace(tags.Title) ? $"Track {Info.Index}" : tags.Title;
    }
}
