using System.Diagnostics;
using System.IO;
using System.Windows;

namespace TVideo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog();

        dialog.InitialDirectory = Environment.CurrentDirectory;
        dialog.Filter = "MKV (*.mkv)|*.mkv";
        dialog.Multiselect = false;

        var result = dialog.ShowDialog();
        if (result == true)
        {
            var directory = Path.GetDirectoryName(dialog.FileName);
            var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
            var extension = Path.GetExtension(dialog.FileName);
            var newFileName = $"{directory}{fileName}_{DateTime.Now:yyyy-MM-dd-HH-mm}{extension}";

            var args = @$"-i ""{dialog.FileName}"" -map 0 -c:v copy -c:a ac3 -b:a 640k -c:s copy ""{newFileName}""";

            var info = new ProcessStartInfo
            {
                FileName = Environment.CurrentDirectory + "\\FFMPEG\\ffmpeg.exe",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = Process.Start(info))
            {
                process.WaitForExit();

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                MessageBox.Show(output + Environment.NewLine + error);
            }
        }
    }
}