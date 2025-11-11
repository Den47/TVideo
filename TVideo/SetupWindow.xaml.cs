using System.Windows;
using TVideo.Info;

namespace TVideo;

public partial class SetupWindow : Window
{
    public SetupWindow(FFProbeResult source)
    {
        DataContext = this;
        Streams = source.Streams.ToArray();

        InitializeComponent();
    }

    public IEnumerable<StreamInfo> Streams { get; }
}
