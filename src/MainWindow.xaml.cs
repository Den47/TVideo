using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using TVideo.Info;

namespace TVideo;

public partial class MainWindow : Window
{
    private readonly List<Process> _processes = [];

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Button1_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            // InitialDirectory = Environment.CurrentDirectory;
            Filter = "MKV (*.mkv)|*.mkv",
            Multiselect = false
        };

        var result = dialog.ShowDialog();
        if (result != true)
            return;

        GetMediaInfo(dialog.FileName);
    }

    private void Button2_Click(object sender, RoutedEventArgs e)
    {
        var dialogLoad = new OpenFileDialog
        {
            // InitialDirectory = Environment.CurrentDirectory;
            Filter = "MKV (*.mkv)|*.mkv",
            Multiselect = false,
        };

        var result = dialogLoad.ShowDialog();
        if (result != true)
            return;

        var dialogSave = new OpenFolderDialog
        {
            Multiselect = false,
        };

        var hasOutput = dialogSave.ShowDialog();

        Convert(dialogLoad.FileName, hasOutput == true ? dialogSave.FolderName : null);
    }

    private static ProcessStartInfo CreateProcessStartInfo(string fileName, string args)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
    }

    private void GetMediaInfo(string selectedFileName)
    {
        var args = @$"-v error -select_streams a -show_entries stream=index,codec_name,codec_type,codec_tag_string,channels,channel_layout:stream_tags=language,title -of json ""{selectedFileName}""";

        using var process = new Process
        {
            StartInfo = CreateProcessStartInfo($"{Environment.CurrentDirectory}\\FFMPEG\\ffprobe.exe", args)
        };

        _processes.Add(process);

        process.Start();
        process.WaitForExit();

        _processes.Remove(process);

        var output = process.StandardOutput.ReadToEnd();
        var result = JsonSerializer.Deserialize<FFProbeResult>(output);
        if (result == null)
        {
            MessageBox.Show("Failed");
            return;
        }

        var setup = new SetupWindow(result);
        setup.ShowDialog();
    }

    private void Convert(string selectedFileName, string? outputDirectory = null)
    {
        var directory = outputDirectory ?? Path.GetDirectoryName(selectedFileName);
        var fileName = Path.GetFileNameWithoutExtension(selectedFileName);
        var extension = Path.GetExtension(selectedFileName);
        var newFileName = Path.Combine(directory, $"{fileName}_{DateTime.Now:yyyy-MM-dd-HH-mm}{extension}");

        var args = new StringBuilder();
        args.Append(@$"-i ""{selectedFileName}"" ");
        args.Append("-map 0 ");
        args.Append("-c:v copy ");
        args.Append("-c:a ac3 ");
        args.Append("-b:a 640k ");
        args.Append("-c:s copy ");
        args.Append(@$"""{newFileName}""");

        var startInfo = CreateProcessStartInfo($"{Environment.CurrentDirectory}\\FFMPEG\\ffmpeg.exe", args.ToString());

        Task.Run(() => RunAsync(startInfo));
    }

    private static void HanleMessage(object sender, DataReceivedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.Data))
            return;

        Debug.WriteLine(eventArgs.Data);
    }

    private async Task RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.OutputDataReceived += HanleMessage;
        process.ErrorDataReceived += HanleMessage;

        _processes.Add(process);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        _processes.Remove(process);

        Debug.WriteLine($"ExitCode = {process.ExitCode}");
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        foreach (var process in _processes)
        {
            if (process.HasExited)
                continue;

            try
            {
                process.StandardInput.WriteLine("q");
                process.StandardInput.Flush();

                if (!process.WaitForExit(500))
                {
                    process.Kill(true);
                }
            }
            catch
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            finally
            {
                process.Close();
                process.Dispose();
            }
        }
    }
}
