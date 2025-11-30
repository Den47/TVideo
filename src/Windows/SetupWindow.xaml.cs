using System.Windows;
using System.Windows.Controls.Primitives;
using TVideo.Windows.Classes;

namespace TVideo.Windows;

public partial class SetupWindow : Window
{
    public SetupWindow(Setup setup)
    {
        DataContext = setup;
        SetupItems = setup.Items;

        InitializeComponent();
    }

    public IEnumerable<SetupItem> SetupItems { get; }

    private void ToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        var button = (ToggleButton)sender;
        var setupItem = (SetupItem)button.DataContext;
        setupItem.Remove = button.IsChecked.GetValueOrDefault();
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
