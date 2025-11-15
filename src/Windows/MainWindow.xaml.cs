using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using TVideo.FF;
using TVideo.Windows.Classes;

namespace TVideo.Windows;

public partial class MainWindow : Window
{
    private readonly List<Process> _processes = [];
    private int _total;
    private int _successful;
    private int _failed;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ConvertFileButton_Click(object sender, RoutedEventArgs e)
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

    private void ConvertFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogLoad = new OpenFolderDialog
        {
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

        foreach (var process in _processes)
        {
            //Convert(dialogLoad.FileName, hasOutput == true ? dialogSave.FolderName : null);
        }
    }

    private void MediaButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MKV (*.mkv)|*.mkv",
            Multiselect = false
        };

        var result = dialog.ShowDialog();
        if (result != true)
            return;

        var probeResult = GetProbeResult(dialog.FileName);
        if (probeResult != null)
            ShowSetupDialog(probeResult, canProcess: false);
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

    private static Setup? ShowSetupDialog(ProbeResult probeResult, bool canProcess = true, bool multiSelect = false)
    {
        var setup = new Setup
        {
            CanProcess = canProcess,
            MultiSelect = multiSelect,
            Contexts =
                probeResult.Streams
                    .Select(x => new SetupContext { Info = x })
                    .ToArray()
        };

        var setupWindow = new SetupWindow(setup);
        setupWindow.ShowDialog();

        return setupWindow.DialogResult == true ? setup : null;
    }

    private ProbeResult? GetProbeResult(string selectedFileName)
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

        try
        {
            var result = JsonSerializer.Deserialize<ProbeResult>(output);
            return result;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while reading file: '{selectedFileName}'{Environment.NewLine}{ex.Message}");
            return null;
        }
    }

    private void Convert(string selectedFileName, string? outputDirectory = null)
    {
        var directory = outputDirectory ?? Path.GetDirectoryName(selectedFileName)!;
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

        var probeResult = GetProbeResult(selectedFileName);
        if (probeResult is null)
            return;

        var setup = ShowSetupDialog(probeResult);
        if (setup is null)
            return;

        if (probeResult.Format is not null &&
            double.TryParse(probeResult.Format.Duration, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
        {
            ProcessProgressBar.Value = 0;
            ProcessProgressBar.Maximum = duration;
        }

        _total = 1;
        _successful = 0;
        _failed = 0;
        UpdateCounts();

        Task.Run(() => RunAsync(startInfo));
    }

    private void HanleMessage(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        var message = e.Data;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            OutputTextBlock.Text = message;
            OutputTextBlock.Visibility = message != null ? Visibility.Visible : Visibility.Collapsed;
        }));

        int start = message.IndexOf("time=");
        if (start == -1)
            return;

        start += "time=".Length;
        var end = start;

        while (end < message.Length && !char.IsWhiteSpace(message[end]))
            end++;

        var timeValue = message.Substring(start, end - start).Trim();

        if (!TimeSpan.TryParseExact(timeValue, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out TimeSpan time))
            return;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            ProcessProgressBar.Value = time.TotalSeconds;
        }));
    }

    private async Task RunAsync(ProcessStartInfo convertProcessInfo, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = convertProcessInfo
        };

        process.OutputDataReceived += HanleMessage;
        process.ErrorDataReceived += HanleMessage;

        _processes.Add(process);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        _processes.Remove(process);

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            OutputTextBlock.Text = "All processes finished!";
            ProcessProgressBar.Value = ProcessProgressBar.Maximum;

            if (process.ExitCode == 0)
                _successful++;
            else
                _failed++;

            UpdateCounts();
        }));
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

    private void UpdateCounts()
    {
        var text = $"Completed: {_successful}/{_total}";

        if (_failed > 0)
            text = $"{text}, failed: {_failed}";

        ProgressTextBlock.Text = text;
    }
}
