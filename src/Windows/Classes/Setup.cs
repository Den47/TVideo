using TVideo.FF;

namespace TVideo.Windows.Classes;

public class Setup
{
    public required IReadOnlyCollection<SetupItem> Items { get; init; }

    public bool CanProcess { get; init; }

    public bool MultiSelect { get; init; }

    public bool ApplyToAll { get; set; }

    public bool IsCompatibleWith(IEnumerable<ProbeStream> streams)
    {
        var savedTracks = GetTracks(Items.Select(item => item.Info));
        var currentTracks = GetTracks(streams);

        if (savedTracks.Length != currentTracks.Length)
            return false;

        for (var index = 0; index < savedTracks.Length; index++)
        {
            if (!TracksMatch(savedTracks[index], currentTracks[index]))
                return false;
        }

        return true;
    }

    private static ProbeStream[] GetTracks(IEnumerable<ProbeStream> streams)
    {
        return streams
            .Where(stream => string.Equals(stream.CodecType, "audio", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stream.CodecType, "subtitle", StringComparison.OrdinalIgnoreCase))
            .OrderBy(stream => stream.CodecType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(stream => stream.Index)
            .ToArray();
    }

    private static bool TracksMatch(ProbeStream saved, ProbeStream current)
    {
        return string.Equals(saved.CodecType, current.CodecType, StringComparison.OrdinalIgnoreCase)
            && TextMatches(saved.CodecName, current.CodecName)
            && saved.Channels == current.Channels
            && TextMatches(saved.Tags?.Language, current.Tags?.Language)
            && TextMatches(saved.Tags?.Title, current.Tags?.Title, StringComparison.Ordinal);
    }

    private static bool TextMatches(
        string? left,
        string? right,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, comparison);
    }
}
