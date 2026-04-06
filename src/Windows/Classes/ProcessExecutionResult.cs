namespace TVideo.Windows.Classes;

internal readonly record struct ProcessExecutionResult
{
    public ProcessExecutionResult(int exitCode, string output)
    {
        ExitCode = exitCode;
        Output = output;
    }

    public int ExitCode { get; }
    public string Output { get; }
}
