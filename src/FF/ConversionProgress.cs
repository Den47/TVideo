using System.Globalization;

namespace TVideo.FF;

internal sealed class ConversionProgress
{
    private double? _speed;
    private bool _finished;
    private bool _successful;

    public ConversionProgress(string? duration)
    {
        if (TryReadNumber(duration, out var seconds) && seconds > 0)
            DurationSeconds = seconds;
    }

    public double? DurationSeconds { get; }
    public double ProcessedSeconds { get; private set; }
    public long? OutputBytes { get; private set; }

    public string ProcessedText => $"Processed: {FormatDuration(ProcessedSeconds)} / {FormatDuration(DurationSeconds)}";

    public string OutputSizeText => $"Output Size: {FormatSize(OutputBytes)}";

    public string TimeRemainingText
    {
        get
        {
            if (_successful)
                return "Time Remaining: 0 min 00 sec";
            if (_finished || !DurationSeconds.HasValue || !_speed.HasValue || ProcessedSeconds <= 0)
                return "Time Remaining: —";

            var remaining = Math.Ceiling(Math.Max(0, DurationSeconds.Value - ProcessedSeconds) / _speed.Value);
            if (!double.IsFinite(remaining))
                return "Time Remaining: —";

            return FormattableString.Invariant($"Time Remaining: ≈ {Math.Floor(remaining / 60):0} min {remaining % 60:00} sec");
        }
    }

    public bool ReadLine(string line)
    {
        var separator = line.IndexOf('=');
        if (separator < 0)
            return false;

        var key = line[..separator];
        var value = line[(separator + 1)..].Trim();
        switch (key)
        {
            case "out_time_us":
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
                    ProcessedSeconds = Math.Max(0, microseconds / 1_000_000.0);
                break;
            case "total_size":
                OutputBytes = long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes) && bytes >= 0
                    ? bytes : null;
                break;
            case "speed":
                _speed = TryReadNumber(value.TrimEnd('x'), out var speed) && speed > 0 ? speed : null;
                break;
            case "progress":
                return value is "continue" or "end";
        }

        return false;
    }

    public void Finish(bool successful, long? outputBytes)
    {
        _finished = true;
        _successful = successful;
        OutputBytes = outputBytes ?? OutputBytes;
    }

    private static bool TryReadNumber(string? value, out double number)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && double.IsFinite(number);
    }

    private static string FormatDuration(double? seconds)
    {
        if (!seconds.HasValue)
            return "—";

        var wholeSeconds = Math.Floor(seconds.Value);
        return FormattableString.Invariant($"{Math.Floor(wholeSeconds / 3600):00}:{Math.Floor(wholeSeconds / 60) % 60:00}:{wholeSeconds % 60:00}");
    }

    private static string FormatSize(double? bytes)
    {
        return bytes.HasValue && double.IsFinite(bytes.Value)
            ? (bytes.Value / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture) + " MB"
            : "—";
    }
}
