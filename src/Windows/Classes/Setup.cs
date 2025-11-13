namespace TVideo.Windows.Classes;

public class Setup
{
    public required IReadOnlyCollection<SetupContext> Contexts { get; set; }

    public bool ApplyToAll { get; set; }
}
