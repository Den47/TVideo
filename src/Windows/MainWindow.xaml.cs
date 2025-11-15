using Microsoft.Win32;
using System;
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

    private bool _applyToAll;
    private int _total;
    private int _successful;
    private int _failed;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogLoad = new OpenFileDialog
        {
            Title = "Select the file to convert",
            Filter = "MKV (*.mkv)|*.mkv",
            Multiselect = false,
        };

        var result = dialogLoad.ShowDialog();
        if (result != true)
            return;

        var dialogSave = new OpenFolderDialog
        {
            Title = "Select a folder to save",
            Multiselect = false,
        };

        var output = dialogSave.ShowDialog() == true ? dialogSave.FolderName : null;
        Restart(1);

        _ = StartConvertationAsync([dialogLoad.FileName], output);
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogLoad = new OpenFolderDialog
        {
            Title = "Select the folder with *.mkv files to convert",
            Multiselect = false,
        };

        var result = dialogLoad.ShowDialog();
        if (result != true)
            return;

        var files = Directory.GetFiles(dialogLoad.FolderName, "*.mkv");
        if (files.Length == 0)
        {
            MessageBox.Show("No *.mkv files found!");
            return;
        }

        var dialogSave = new OpenFolderDialog
        {
            Title = "Select a folder to save",
            Multiselect = false,
        };

        var output = dialogSave.ShowDialog() == true ? dialogSave.FolderName : null;
        Restart(files.Length);

        _ = StartConvertationAsync(files, output);
    }

    private async void MediaButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MKV (*.mkv)|*.mkv",
            Multiselect = false
        };

        var result = dialog.ShowDialog();
        if (result != true)
            return;

        var probeResult = await GetProbeResultAsync(dialog.FileName);
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

    private async Task<ProbeResult?> GetProbeResultAsync(string selectedFileName, CancellationToken cancellationToken = default)
    {
        var args = @$"-v error -show_entries format=duration:stream=index,codec_type,codec_name,channels:stream_tags=language,title -of json ""{selectedFileName}""";

        var startInfo = CreateProcessStartInfo($"{Environment.CurrentDirectory}\\FFMPEG\\ffprobe.exe", args);
        var result = await RunProcessAsync(startInfo, handleOutput: false, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<ProbeResult>(result.Output);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while reading file: '{selectedFileName}'{Environment.NewLine}{ex.Message}");
            return null;
        }
    }

    private async Task StartConvertationAsync(string[] files, string? outputDirectory)
    {
        var hasResult = false;

        foreach (var fileName in files)
        {
            var exitCode = await ConvertFileAsync(fileName, outputDirectory, multiSelect: files.Length > 1);
            hasResult |= exitCode == 0;
        }

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (hasResult)
            {
                UpdateCounts();
                OutputTextBlock.Text = "All processes finished!";
                ProcessProgressBar.Value = ProcessProgressBar.Maximum;
            }
            else
            {
                ProgressTextBlock.Text = string.Empty;
                OutputTextBlock.Visibility = Visibility.Collapsed;
            }

            ControlPanel.IsEnabled = true;
        }));
    }

    private async Task<int?> ConvertFileAsync(
        string selectedFileName,
        string? outputDirectory = null,
        bool multiSelect = false,
        CancellationToken cancellationToken = default)
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

        var probeResult = await GetProbeResultAsync(selectedFileName, cancellationToken);
        var cancel = false;

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (probeResult is null)
            {
                ControlPanel.IsEnabled = true;
                cancel = true;
                return;
            }

            if (!_applyToAll)
            {
                var setup = ShowSetupDialog(probeResult, multiSelect: multiSelect);
                if (setup is null)
                {
                    ControlPanel.IsEnabled = true;
                    cancel = true;
                    return;
                }

                _applyToAll = setup.ApplyToAll;
            }

            ProcessProgressBar.Value = 0;

            if (double.TryParse(probeResult.Format?.Duration, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
            {
                ProcessProgressBar.Maximum = duration;
            }
        }));

        if (cancel)
            return null;

        var startInfo = CreateProcessStartInfo($"{Environment.CurrentDirectory}\\FFMPEG\\ffmpeg.exe", args.ToString());
        var result = await RunProcessAsync(startInfo, handleOutput: true, cancellationToken);

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (result.ExitCode == 0)
                _successful++;
            else
                _failed++;

            UpdateCounts();
            OutputTextBlock.Text = "Process finished!";
            ProcessProgressBar.Value = ProcessProgressBar.Maximum;
        }));

        return result.ExitCode;
    }

    private void HanleMessage(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        var message = e.Data;
        var seconds = 0.0;

        var start = message.IndexOf("time=");
        if (start != -1)
        {
            start += "time=".Length;
            var end = start;

            while (end < message.Length && !char.IsWhiteSpace(message[end]))
                end++;

            var timeValue = message.Substring(start, end - start).Trim();

            if (TimeSpan.TryParseExact(timeValue, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out TimeSpan time))
                seconds = time.TotalSeconds;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            OutputTextBlock.Text = message;
            OutputTextBlock.Visibility = message != null ? Visibility.Visible : Visibility.Collapsed;
            ProcessProgressBar.Value = seconds > 0 ? seconds : ProcessProgressBar.Value;
        }));
    }

    private async Task<(int ExitCode, string Output)> RunProcessAsync(ProcessStartInfo convertProcessInfo, bool handleOutput = false, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = convertProcessInfo
        };

        if (handleOutput)
        {
            process.OutputDataReceived += HanleMessage;
            process.ErrorDataReceived += HanleMessage;
        }

        _processes.Add(process);

        process.Start();

        if (handleOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync(cancellationToken);

        _processes.Remove(process);

        if (handleOutput)
        {
            process.OutputDataReceived -= HanleMessage;
            process.ErrorDataReceived -= HanleMessage;
        }

        var output = handleOutput ? string.Empty : await process.StandardOutput.ReadToEndAsync(cancellationToken);
        return (process.ExitCode, output);
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

    private void Restart(int total)
    {
        _applyToAll = false;
        _total = total;
        _successful = 0;
        _failed = 0;
        ControlPanel.IsEnabled = false;
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        var text = $"Completed: {_successful}/{_total}";

        if (_failed > 0)
            text = $"{text}, failed: {_failed}";

        ProgressTextBlock.Text = text;
    }
}
