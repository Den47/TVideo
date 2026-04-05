namespace TVideo.Windows.Classes;

internal record ProcessResult
{
    public bool Cancel { get; }

    public int ExitCode { get; }

    public ProcessResult(int exitCode)
    {
        ExitCode = exitCode;
    }

    public ProcessResult(bool cancel)
    {
        Cancel = cancel;
    }
}
