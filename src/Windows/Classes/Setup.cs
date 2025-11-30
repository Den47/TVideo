namespace TVideo.Windows.Classes;

public class Setup
{
    public required IReadOnlyCollection<SetupItem> Items { get; init; }

    public bool CanProcess { get; init; }

    public bool MultiSelect { get; init; }

    public bool ApplyToAll { get; set; }
}
