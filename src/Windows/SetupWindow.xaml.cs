using System.Windows;
using System.Windows.Controls.Primitives;
using TVideo.Windows.Classes;

namespace TVideo.Windows;

public partial class SetupWindow : Window
{
    public SetupWindow(IReadOnlyCollection<SetupContext> source)
    {
        DataContext = this;
        SetupContexts = source;

        InitializeComponent();
    }

    public IEnumerable<SetupContext> SetupContexts { get; }

    private void ToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        var button = (ToggleButton)sender;
        var setupContext = (SetupContext)button.DataContext;
        setupContext.Remove = button.IsChecked.GetValueOrDefault();
    }
}
