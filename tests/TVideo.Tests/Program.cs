using TVideo.Tests;

var previousDirectory = Environment.CurrentDirectory;
try
{
    Environment.CurrentDirectory = AppContext.BaseDirectory;
    using var files = new TemporaryDirectory();
    Console.WriteLine($"Temporary files: {files.Path}");
    var suite = new TestSuite(files.Path);
    var result = await suite.Run();
    files.Dispose();
    Console.WriteLine("Temporary files deleted.");
    return result;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
finally
{
    Environment.CurrentDirectory = previousDirectory;
}
