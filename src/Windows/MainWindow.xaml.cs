using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using TVideo.FF;
using TVideo.Windows.Classes;

namespace TVideo.Windows;

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

    private ProbeResult? GetMediaInfo(string selectedFileName)
    {
        var args = @$"-v error -show_entries format=duration:stream=index,codec_type,codec_name,channels:stream_tags=language,title -of json ""{selectedFileName}""";

        using var process = new Process
        {
            StartInfo = CreateProcessStartInfo($"{Environment.CurrentDirectory}\\FFMPEG\\ffprobe.exe", args)
        };

        _processes.Add(process);

        process.Start();
        process.WaitForExit();

        _processes.Remove(process);

        var output = process.StandardOutput.ReadToEnd();
        var result = JsonSerializer.Deserialize<ProbeResult>(output);
        if (result == null)
        {
            MessageBox.Show("Failed");
            return null;
        }

        var source = result.Streams.Select(x => new SetupContext { Info = x }).ToArray();
        var setup = new SetupWindow(source);
        setup.ShowDialog();

        return result;
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

        var mediaInfo = GetMediaInfo(selectedFileName);

        if (mediaInfo?.Format != null && double.TryParse(mediaInfo.Format.Duration, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
        {
            ProcessProgressBar.Value = 0;
            ProcessProgressBar.Maximum = duration;
        }

        Task.Run(() => RunAsync(startInfo));
    }

    private void HanleMessage(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        var message = e.Data;
        Debug.WriteLine(e.Data);

        int start = message.IndexOf("time=");
        if (start != -1)
        {
            start += "time=".Length;
            var end = start;

            while (end < message.Length && !char.IsWhiteSpace(message[end]))
                end++;

            var timeValue = message.Substring(start, end - start).Trim();

            if (TimeSpan.TryParseExact(timeValue, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out TimeSpan time))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    InfoTextBlock.Text = message;
                    ProcessProgressBar.Value = time.TotalSeconds;
                }));
            }
        }
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
