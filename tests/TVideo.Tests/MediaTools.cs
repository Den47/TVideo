using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TVideo.FF;
using TVideo.Windows.Classes;

namespace TVideo.Tests;

internal static class MediaTools
{
    public static ProcessStartInfo Command(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "FFMPEG", executable))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        return start;
    }

    public static async Task<string> Run(ProcessStartInfo start)
    {
        using var process = new Process { StartInfo = start };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await output;
            throw new TimeoutException($"Process exceeded 15 seconds: {start.FileName}\n{await error}");
        }

        var standardOutput = await output;
        var standardError = await error;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Process exited with {process.ExitCode}: {standardError}");
        return standardOutput;
    }

    public static async Task<Setup> ReadSetup(string path)
    {
        var json = await Run(Utils.CreateFFProbeStartInfo(path));
        var result = JsonSerializer.Deserialize<ProbeResult>(json)!;
        return new Setup
        {
            Items = result.Streams
                .Where(stream => stream.CodecType is "audio" or "subtitle")
                .Select(stream => new SetupItem(stream))
                .ToArray(),
        };
    }

    public static async Task<MediaStream[]> Inspect(string path)
    {
        var json = await Run(Command("ffprobe.exe", "-v", "error", "-show_streams",
            "-show_packets", "-show_data_hash", "sha256", "-of", "json", path));
        using var document = JsonDocument.Parse(json);
        var packets = document.RootElement.GetProperty("packets").EnumerateArray().ToArray();
        return document.RootElement.GetProperty("streams").EnumerateArray().Select(stream =>
        {
            var index = stream.GetProperty("index").GetInt32();
            stream.TryGetProperty("tags", out var tags);
            return new MediaStream(
                Read(stream, "codec_type"), Read(stream, "codec_name"),
                Read(tags, "language"), Read(tags, "filename"), Read(tags, "mimetype"),
                Read(stream, "extradata_hash"),
                packets.Where(packet => packet.GetProperty("stream_index").GetInt32() == index)
                    .Select(packet => Read(packet, "data_hash")).ToArray());
        }).ToArray();
    }

    private static string Read(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    public static async Task<string> CreateFixture(string directory, bool includeExtras)
    {
        var output = Path.Combine(directory, includeExtras ? "tracks.mkv" : "minimal.mkv");
        var args = new List<string>
        {
            "-v", "error", "-n", "-f", "lavfi", "-i", "color=size=16x16:rate=25:duration=0.04",
        };
        var count = includeExtras ? 3 : 1;
        for (var index = 0; index < count; index++)
            args.AddRange(["-f", "lavfi", "-i", $"sine=frequency={440 + index * 110}:sample_rate=48000:duration=0.04"]);

        if (includeExtras)
        {
            for (var index = 0; index < count; index++)
            {
                var subtitle = Path.Combine(directory, $"subtitle{index}.srt");
                await File.WriteAllTextAsync(subtitle, $"1\n00:00:00,000 --> 00:00:00,040\nTrack {index}\n");
                args.AddRange(["-i", subtitle]);
            }
        }

        args.AddRange(["-map", "0:v", "-c:v", "ffv1", "-c:a", "flac"]);
        string[] languages = ["eng", "rus", "fra"];
        for (var index = 0; index < count; index++)
            args.AddRange(["-map", $"{index + 1}:a", $"-metadata:s:a:{index}", $"language={languages[index]}"]);

        if (includeExtras)
        {
            args.AddRange(["-c:s", "srt"]);
            for (var index = 0; index < count; index++)
                args.AddRange(["-map", $"{index + count + 1}:s", $"-metadata:s:s:{index}", $"language={languages[index]}"]);

            var attachment = Path.Combine(directory, "notes.txt");
            await File.WriteAllTextAsync(attachment, "Test attachment");
            args.AddRange(["-attach", attachment, "-metadata:s:t:0", "mimetype=text/plain"]);
            var font = Path.Combine(directory, "synthetic.ttf");
            await File.WriteAllBytesAsync(font, [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
            args.AddRange(["-attach", font, "-metadata:s:t:1", "mimetype=application/x-truetype-font"]);
        }

        args.Add(output);
        await Run(Command("ffmpeg.exe", args.ToArray()));
        return output;
    }
}

internal sealed record MediaStream(
    string Type, string Codec, string Language, string FileName, string MimeType,
    string ExtraDataHash, string[] PacketHashes);
