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
    private ConversionProgress? _conversionProgress;

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

        SelectOutputAndConvert([dialogLoad.FileName]);
    }

    private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogLoad = new OpenFileDialog
        {
            Title = "Select the files to convert",
            Filter = "MKV (*.mkv)|*.mkv",
            Multiselect = true,
        };

        var resultOpen = dialogLoad.ShowDialog();
        if (resultOpen != true)
            return;

        SelectOutputAndConvert(dialogLoad.FileNames);
    }

    private void FileDropArea_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedFiles(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FileDropArea_Drop(object sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e);
        e.Effects = files is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (files is not null)
            SelectOutputAndConvert(files);
    }

    private string[]? GetDroppedFiles(DragEventArgs e)
    {
        if (!ControlPanel.IsEnabled || !e.AllowedEffects.HasFlag(DragDropEffects.Copy) ||
            !e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files)
            return null;

        return files.All(file => File.Exists(file) &&
            string.Equals(Path.GetExtension(file), ".mkv", StringComparison.OrdinalIgnoreCase))
            ? files
            : null;
    }

    private void SelectOutputAndConvert(string[] files)
    {
        var dialogSave = new OpenFolderDialog
        {
            Title = "Select a folder to save",
            InitialDirectory = Path.GetDirectoryName(files[0])!,
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
        var addTimestamp = HasOutputNameCollisions(files, outputDirectory);
        var hasResult = false;

        foreach (var fileName in files)
        {
            var result = await ConvertFileAsync(fileName, outputDirectory, addTimestamp, multiSelect: files.Length > 1);
            if (result.Cancel)
                break;

            hasResult |= result.ExitCode == 0;
        }

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (hasResult)
            {
                UpdateCounts();
                ProcessProgressBar.Value = ProcessProgressBar.Maximum;
            }
            else
            {
                ProgressTextBlock.Text = string.Empty;
            }

            OutputPanel.Visibility = Visibility.Collapsed;
            ControlPanel.IsEnabled = true;
        }));
    }

    private async Task<FlowResult> ConvertFileAsync(
        string selectedFileName,
        string? outputDirectory = null,
        bool addTimestamp = false,
        bool multiSelect = false,
        CancellationToken cancellationToken = default)
    {
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

            _conversionProgress = new ConversionProgress(probeResult.Format?.Duration);
            CurrentFileTextBlock.Text = $"Current File: {Path.GetFileName(selectedFileName)}";
            CurrentFileTextBlock.ToolTip = Path.GetFileName(selectedFileName);
            OutputPanel.Visibility = Visibility.Visible;
            UpdateProgressDetails();

            if (double.TryParse(probeResult.Format?.Duration, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
            {
                ProcessProgressBar.Maximum = duration;
            }
        }));

        if (cancel || setup is null)
            return new FlowResult(cancel: true);

        var newFileName = GetOutputFilePath(selectedFileName, outputDirectory, addTimestamp);
        var startInfo = Utils.CreateFFMpegStartInfo(selectedFileName, newFileName, setup);
        var execResult = await RunProcessAsync(startInfo, handleOutput: true, cancellationToken);

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (execResult.ExitCode == 0)
                _successful++;
            else
                _failed++;

            UpdateCounts();
            var outputFile = new FileInfo(newFileName);
            _conversionProgress?.Finish(execResult.ExitCode == 0, outputFile.Exists ? outputFile.Length : null);
            UpdateProgressDetails();
            ProcessProgressBar.Value = ProcessProgressBar.Maximum;
        }));

        return new FlowResult(execResult.ExitCode);
    }

    internal static bool HasOutputNameCollisions(string[] files, string? outputDirectory)
    {
        return files.Any(file => Path.Exists(Path.Combine(
            outputDirectory ?? Path.GetDirectoryName(file)!, Path.GetFileName(file))));
    }

    internal static string GetOutputFilePath(string inputFilePath, string? outputDirectory, bool addTimestamp = false)
    {
        var directory = outputDirectory ?? Path.GetDirectoryName(inputFilePath)!;
        var originalPath = Path.Combine(directory, Path.GetFileName(inputFilePath));
        if (!addTimestamp && !Path.Exists(originalPath))
            return originalPath;

        var fileName = Path.GetFileNameWithoutExtension(inputFilePath);
        var extension = Path.GetExtension(inputFilePath);
        var outputName = $"{fileName}_{DateTime.Now:yyyy-MM-dd-HH-mm}";
        var outputPath = Path.Combine(directory, $"{outputName}{extension}");
        var suffix = 1;

        while (File.Exists(outputPath) || Directory.Exists(outputPath))
        {
            outputPath = Path.Combine(directory, $"{outputName}_{suffix++}{extension}");
        }

        return outputPath;
    }

    private void HandleMessage(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_conversionProgress is null || !_conversionProgress.ReadLine(e.Data))
                return;

            UpdateProgressDetails();
            var seconds = _conversionProgress.ProcessedSeconds;
            ProcessProgressBar.Value = seconds > 0 ? seconds : ProcessProgressBar.Value;
        }));
    }

    private void UpdateProgressDetails()
    {
        if (_conversionProgress is null)
            return;

        ProcessedTextBlock.Text = _conversionProgress.ProcessedText;
        OutputSizeTextBlock.Text = _conversionProgress.OutputSizeText;
        TimeRemainingTextBlock.Text = _conversionProgress.TimeRemainingText;
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
        }

        _processes.Add(process);

        process.Start();

        var outputTask = handleOutput
            ? Task.FromResult(string.Empty)
            : process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (handleOutput)
        {
            process.BeginOutputReadLine();
        }

        await process.WaitForExitAsync(cancellationToken);

        _processes.Remove(process);

        if (handleOutput)
        {
            process.OutputDataReceived -= HandleMessage;
        }

        var output = await outputTask;
        await errorTask;
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
        _conversionProgress = null;
        OutputPanel.Visibility = Visibility.Collapsed;
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
