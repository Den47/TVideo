using TVideo.FF;

namespace TVideo.Windows.Classes;

public class SetupContext
{
    public required ProbeStream Info { get; init; }

    public bool Convert { get; set; } = true;

    public bool Remove { get; set; }

    public bool IsAudio => string.Equals(Info.CodecType, "audio", StringComparison.OrdinalIgnoreCase);

    public bool IsVideo => string.Equals(Info.CodecType, "video", StringComparison.OrdinalIgnoreCase);

    public bool IsSubtitle => string.Equals(Info.CodecType, "subtitle", StringComparison.OrdinalIgnoreCase);

    public bool CanRemove => IsAudio || IsSubtitle;

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

        var language = string.IsNullOrWhiteSpace(tags.Language) ? "[Unknown]" : tags.Language;
        var title = string.IsNullOrWhiteSpace(tags.Title) ? "[Untitled]" : tags.Title;

        return $"{language}  {title}";
    }
}
