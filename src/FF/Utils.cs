using System.Text;
using TVideo.Windows.Classes;

namespace TVideo.FF;

internal static class Utils
{
    public static string PrepareParams(Setup setup, string inputFilePath, string outputFilePath)
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
        var index = 0;
        var codec = "ac3";

        var @params = setup.Items
            .Where(x => x.IsAudio)
            .OrderBy(x => x.Info!.Index)
            .Select(x => (Item: x, Index: index++))
            .Select(x =>
            {
                if (x.Item.Remove)
                {
                    return $"-map -0:a:{x.Index}";
                }

                return x.Item.Convert
                    //? $"-map 0:a:{x.Index} -c:a:{x.Index} {codec} -b:a:{x.Index} 640k"
                    //? $"-map 0:a:{x.Index} -c:a:{x.Index} ac3 -b:a:{x.Index} 192k -ac 2"
                    ? $"-map 0:a:{x.Index} -c:a:{x.Index} {codec} -b:a:{x.Index} 384k"
                    : $"-map 0:a:{x.Index} -c:a:{x.Index} copy";
            });

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
            .OrderBy(x => x.Info!.Index)
            .Select(x => (Item: x, Index: index++))
            .Where(x => !x.Item.Remove)
            .Select(x => $"-map 0:s:{x.Index} -c:s:{x.Index} copy");

        var result = string.Join(' ', @params);
        
        return result.Length > 0
            ? $"{result} "
            : result;
    }
}
