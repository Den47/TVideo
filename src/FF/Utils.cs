using System.Diagnostics;
using System.Text;
using TVideo.Windows.Classes;

namespace TVideo.FF;

internal static class Utils
{
    public static ProcessStartInfo CreateFFProbeStartInfo(string inputFilePath)
    {
        var fileName = $"{Environment.CurrentDirectory}\\FFMPEG\\ffprobe.exe";
        var args = @$"-v error -show_entries format=duration:stream=index,codec_type,codec_name,channels:stream_tags=language,title -of json ""{inputFilePath}""";
        return CreateProcessStartInfo(fileName, args);
    }

    public static ProcessStartInfo CreateFFMpegStartInfo(string inputFilePath, string outputFilePath, Setup setup)
    {
        var args = PrepareParams(inputFilePath, outputFilePath, setup);
        var fileName = $"{Environment.CurrentDirectory}\\FFMPEG\\ffmpeg.exe";
        return CreateProcessStartInfo(fileName, args);
    }

    private static string PrepareParams(string inputFilePath, string outputFilePath, Setup setup)
    {
        var args = new StringBuilder();
        args.Append($"-i \"{inputFilePath}\" ");
        args.Append(PrepareVideoParameters());
        args.Append(PrepareAudioParameters(setup));
        args.Append(PrepareSubtitleParameters(setup));
        args.Append($"\"{outputFilePath}\"");
        return args.ToString();
    }

    private static string PrepareVideoParameters()
    {
        return "-map 0:v:0 -c:v copy ";
    }

    private static string PrepareAudioParameters(Setup setup)
    {
        var indexOfAudio = 0;

        var indexedAudio = setup.Items
            .Where(x => x.IsAudio)
            .OrderBy(x => x.Info.Index)
            .Select(x => (Item: x, Index: indexOfAudio++))
            .Where(x => !x.Item.Remove)
            .ToArray();

        if (indexedAudio.Length == 0)
            return string.Empty;

        var currentIndex = 0;
        var @params = new string[indexedAudio.Length];

        foreach (var track in indexedAudio)
        {
            var index = track.Index;
            var codec = track.Item.NewCodec;

            var command = track.Item.Convert
                ? $"-map 0:a:{index} -c:a:{currentIndex} {codec} -b:a:{currentIndex} 640k"
                : $"-map 0:a:{index} -c:a:{currentIndex} copy";

            @params[currentIndex++] = command;
        }

        var result = string.Join(' ', @params);
        if (result.Length > 0)
            return $"{result} ";

        return result;
    }

    private static string PrepareSubtitleParameters(Setup setup)
    {
        var index = 0;

        var @params = setup.Items
            .Where(x => x.IsSubtitle)
            .OrderBy(x => x.Info.Index)
            .Select(x => (Item: x, Index: index++))
            .Where(x => !x.Item.Remove)
            .Select(x => $"-map 0:s:{x.Index}");

        var result = string.Join(' ', @params);

        return result.Length > 0
            ? $"{result} -c:s copy "
            : result;
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
}
