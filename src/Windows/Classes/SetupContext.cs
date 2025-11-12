using TVideo.FF;

namespace TVideo.Windows.Classes;

public class SetupContext
{
    public required StreamInfo Info { get; init; }

    public bool Convert { get; set; } = true;

    public bool Remove { get; set; }
}
