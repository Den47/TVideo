using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using TVideo.FF;
using TVideo.Windows.Classes;

namespace TVideo.Windows;

public partial class MainWindow
{
    private readonly List<Process> _processes = [];

    private int _total;
    private int _successful;
    private int _failed;

    private Setup? _currentSetup;

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
        Reset(1);

        _ = StartConvertingAsync([dialogLoad.FileName], output);
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogLoad = new OpenFolderDialog
        {
            Title = "Select the folder with *.mkv files to convert",
            Multiselect = false,
        };

        var resultOpen = dialogLoad.ShowDialog();
        if (resultOpen != true)
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

        var resultSave = dialogSave.ShowDialog();
        if (resultSave != true)
            return;

        Reset(files.Length);
        _ = StartConvertingAsync(files, dialogSave.FolderName);
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

    private static Setup? ShowSetupDialog(ProbeResult probeResult, bool canProcess = true, bool multiSelect = false)
    {
        var setup = new Setup
        {
            CanProcess = canProcess,
            MultiSelect = multiSelect,
            Items = probeResult.Streams
                .Select(x => new SetupItem(x))
                .Where(x => x.IsAudio || x.IsSubtitle)
                .ToArray(),
        };

        var setupWindow = new SetupWindow(setup);
        setupWindow.ShowDialog();

        return setupWindow.DialogResult == true ? setup : null;
    }

    private async Task<ProbeResult?> GetProbeResultAsync(string selectedFileName, CancellationToken cancellationToken = default)
    {
        var startInfo = Utils.CreateFFProbeStartInfo(selectedFileName);
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

    private async Task StartConvertingAsync(string[] files, string? outputDirectory)
    {
        var hasResult = false;

        foreach (var fileName in files)
        {
            var result = await ConvertFileAsync(fileName, outputDirectory, multiSelect: files.Length > 1);
            if (result.Cancel)
                break;

            hasResult |= result.ExitCode == 0;
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

    private async Task<FlowResult> ConvertFileAsync(
        string selectedFileName,
        string? outputDirectory = null,
        bool multiSelect = false,
        CancellationToken cancellationToken = default)
    {
        var directory = outputDirectory ?? Path.GetDirectoryName(selectedFileName)!;
        var fileName = Path.GetFileNameWithoutExtension(selectedFileName);
        var extension = Path.GetExtension(selectedFileName);
        var newFileName = Path.Combine(directory, $"{fileName}_{DateTime.Now:yyyy-MM-dd-HH-mm}{extension}");

        var probeResult = await GetProbeResultAsync(selectedFileName, cancellationToken);
        var setup = _currentSetup;
        var cancel = false;

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (probeResult is null)
            {
                ControlPanel.IsEnabled = true;
                cancel = true;
                return;
            }

            if (setup is not null && !setup.IsCompatibleWith(probeResult.Streams))
            {
                setup = null;
                _currentSetup = null;
            }

            if (setup is null)
            {
                setup = ShowSetupDialog(probeResult, multiSelect: multiSelect);

                if (setup is null)
                {
                    ControlPanel.IsEnabled = true;
                    cancel = true;
                    return;
                }

                _currentSetup = setup.ApplyToAll ? setup : null;
            }

            ProcessProgressBar.Value = 0;

            if (double.TryParse(probeResult.Format?.Duration, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
            {
                ProcessProgressBar.Maximum = duration;
            }
        }));

        if (cancel || setup is null)
            return new FlowResult(cancel: true);

        var startInfo = Utils.CreateFFMpegStartInfo(selectedFileName, newFileName, setup);
        var execResult = await RunProcessAsync(startInfo, handleOutput: true, cancellationToken);

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (execResult.ExitCode == 0)
                _successful++;
            else
                _failed++;

            UpdateCounts();
            OutputTextBlock.Text = "Process finished!";
            ProcessProgressBar.Value = ProcessProgressBar.Maximum;
        }));

        return new FlowResult(execResult.ExitCode);
    }

    private void HandleMessage(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        var message = e.Data;
        var seconds = 0.0;

        var start = message.IndexOf("time=", StringComparison.Ordinal);
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
            OutputTextBlock.Visibility = Visibility.Visible;
            ProcessProgressBar.Value = seconds > 0 ? seconds : ProcessProgressBar.Value;
        }));
    }

    private async Task<ProcessExecutionResult> RunProcessAsync(
        ProcessStartInfo convertProcessInfo,
        bool handleOutput = false,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = convertProcessInfo;

        if (handleOutput)
        {
            process.OutputDataReceived += HandleMessage;
            process.ErrorDataReceived += HandleMessage;
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
            process.OutputDataReceived -= HandleMessage;
            process.ErrorDataReceived -= HandleMessage;
        }

        var output = handleOutput ? string.Empty : await process.StandardOutput.ReadToEndAsync(cancellationToken);
        return new ProcessExecutionResult(process.ExitCode, output);
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

    private void Reset(int total)
    {
        _currentSetup = null;
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
        {
            text = $"{text}, failed: {_failed}";
        }

        ProgressTextBlock.Text = text;
    }
}
