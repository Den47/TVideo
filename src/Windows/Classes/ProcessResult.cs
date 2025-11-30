namespace TVideo.Windows.Classes;

internal record ProcessResult
{
    public bool Cancel { get; init; }

    public int ExitCode { get; init; }

    public ProcessResult(int exitCode)
    {
        ExitCode = exitCode;
    }

    public ProcessResult(bool cancel)
    {
        Cancel = cancel;
    }
}
