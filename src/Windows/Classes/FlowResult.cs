namespace TVideo.Windows.Classes;

internal readonly record struct FlowResult
{
    public FlowResult(int exitCode)
    {
        ExitCode = exitCode;
    }

    public FlowResult(bool cancel)
    {
        Cancel = cancel;
    }

    public bool Cancel { get; }
    public int ExitCode { get; }
}
