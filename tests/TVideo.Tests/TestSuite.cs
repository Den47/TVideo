using System.IO;
using System.Security.Cryptography;
using TVideo.FF;
using TVideo.Windows;
using TVideo.Windows.Classes;

namespace TVideo.Tests;

internal sealed class TestSuite(string directory)
{
    private readonly List<(string Name, Func<Task> Run)> _tests = [];
    private string _fixture = string.Empty;
    private string _minimal = string.Empty;
    private MediaStream[] _original = [];

    public async Task<int> Run()
    {
        RegisterLogicTests();
        _fixture = await MediaTools.CreateFixture(directory, includeExtras: true);
        _minimal = await MediaTools.CreateFixture(directory, includeExtras: false);
        _original = await MediaTools.Inspect(_fixture);
        Console.WriteLine($"Fixtures: {new FileInfo(_fixture).Length} and {new FileInfo(_minimal).Length} bytes; one 16x16 video frame each.");
        RegisterMediaTests();

        var failed = 0;
        foreach (var test in _tests)
        {
            try
            {
                await test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {test.Name}\n{exception}");
            }
        }

        Console.WriteLine($"{_tests.Count - failed}/{_tests.Count} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private void Add(string name, Action run)
    {
        _tests.Add((name, () => { run(); return Task.CompletedTask; }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private void RegisterLogicTests()
    {
        Add("Apply to all accepts identical tracks", () =>
            Require(SavedSetup().IsCompatibleWith(Tracks()), "Identical tracks rejected."));
        Add("Apply to all checks removed tracks too", () =>
        {
            var setup = SavedSetup();
            setup.Items.First().Remove = true;
            Require(!setup.IsCompatibleWith(Tracks().Skip(1)), "Removed source track was ignored.");
        });
        Add("Apply to all ignores video and absolute index shifts", () =>
        {
            var tracks = Tracks();
            foreach (var track in tracks)
                track.Index += 2;
            Require(SavedSetup().IsCompatibleWith(tracks.Append(new ProbeStream { CodecType = "video" })),
                "Unrelated video or absolute index changed compatibility.");
        });
        Add("Apply to all normalizes language, codec and whitespace", () =>
        {
            var tracks = Tracks();
            foreach (var track in tracks)
            {
                track.CodecName = $" {track.CodecName!.ToUpperInvariant()} ";
                track.Tags!.Language = $" {track.Tags.Language!.ToUpperInvariant()} ";
                track.Tags.Title = " Main ";
            }
            Require(SavedSetup().IsCompatibleWith(tracks), "Equivalent metadata rejected.");
        });

        foreach (var type in new[] { "audio", "subtitle" })
        {
            Add($"Apply to all rejects missing {type}", () =>
            {
                var tracks = Tracks().ToList();
                tracks.Remove(tracks.First(track => track.CodecType == type));
                Require(!SavedSetup().IsCompatibleWith(tracks), "Missing track accepted.");
            });
            Add($"Apply to all rejects additional {type}", () =>
                Require(!SavedSetup().IsCompatibleWith(Tracks().Append(Track(9, type, "deu"))), "Additional track accepted."));
            Add($"Apply to all rejects reordered {type} languages", () =>
            {
                var tracks = Tracks();
                var sameType = tracks.Where(track => track.CodecType == type).ToArray();
                (sameType[0].Index, sameType[1].Index) = (sameType[1].Index, sameType[0].Index);
                Require(!SavedSetup().IsCompatibleWith(tracks), "Reordered languages accepted.");
            });
        }

        (string Name, Action<ProbeStream> Change)[] changes =
        [
            ("codec", track => track.CodecName = "dts"),
            ("channels", track => track.Channels = 6),
            ("language", track => track.Tags!.Language = "deu"),
            ("title", track => track.Tags!.Title = "Commentary"),
            ("missing tags", track => track.Tags = null),
        ];
        foreach (var change in changes)
        {
            Add($"Apply to all rejects changed {change.Name}", () =>
            {
                var tracks = Tracks();
                change.Change(tracks[0]);
                Require(!SavedSetup().IsCompatibleWith(tracks), "Changed metadata accepted.");
            });
        }

        Add("Output name skips existing files and directories", () =>
        {
            var folder = Path.Combine(directory, "names");
            Directory.CreateDirectory(folder);
            var input = Path.Combine(folder, "source.mkv");
            var first = MainWindow.GetOutputFilePath(input, folder);
            Require(!File.Exists(first) && Path.GetExtension(first) == ".mkv", "Invalid initial name.");
            var now = DateTime.Now;
            for (var minute = -1; minute <= 1; minute++)
            {
                var stem = Path.Combine(folder, $"source_{now.AddMinutes(minute):yyyy-MM-dd-HH-mm}");
                File.WriteAllText(stem + ".mkv", "Keep");
                File.WriteAllText(stem + "_1.mkv", "Keep");
                Directory.CreateDirectory(stem + "_2.mkv");
            }
            var next = MainWindow.GetOutputFilePath(input, folder);
            Require(next.EndsWith("_3.mkv", StringComparison.Ordinal), "Occupied names not skipped.");
            Require(!Path.Exists(next), "Selected path already exists.");
            Require(Path.GetDirectoryName(MainWindow.GetOutputFilePath(input, null)) == folder,
                "Default destination differs from input directory.");
        });
        Add("Temporary files are deleted when a test throws", () =>
        {
            string? path = null;
            try
            {
                using var files = new TemporaryDirectory();
                path = files.Path;
                File.WriteAllText(Path.Combine(path, "fixture.mkv"), "Temporary");
                throw new InvalidOperationException("Intentional cleanup test");
            }
            catch (InvalidOperationException exception) when (exception.Message == "Intentional cleanup test")
            {
                Require(path is not null && !Directory.Exists(path), "Failed test left temporary files.");
            }
        });
    }

    private void RegisterMediaTests()
    {
        _tests.Add(("Selected audio becomes AC3 while video stays unchanged", async () =>
        {
            var setup = await MediaTools.ReadSetup(_fixture);
            var result = await Convert(_fixture, setup);
            Require(result.Count(stream => stream.Type == "audio") == 3, "Audio tracks lost.");
            Require(result.Where(stream => stream.Type == "audio").All(stream => stream.Codec == "ac3"), "Audio not converted to AC3.");
            SamePayload(_original.Single(stream => stream.Type == "video"), result.Single(stream => stream.Type == "video"));
        }));
        _tests.Add(("Mixed audio conversion and copying uses correct output indices", async () =>
        {
            var setup = await MediaTools.ReadSetup(_fixture);
            var audio = setup.Items.Where(item => item.IsAudio).ToArray();
            audio[0].Remove = true;
            audio[2].Convert = false;
            var result = (await Convert(_fixture, setup)).Where(stream => stream.Type == "audio").ToArray();
            Require(result.Length == 2 && result[0].Language == "rus" && result[0].Codec == "ac3", "Wrong track converted.");
            Require(result[1].Language == "fra" && result[1].Codec == "flac", "Wrong track copied.");
            SamePayload(_original.Single(stream => stream.Type == "audio" && stream.Language == "fra"), result[1]);
        }));

        foreach (var type in new[] { "audio", "subtitle" })
        {
            foreach (var position in new[] { 0, 1, 2, -1 })
            {
                var name = position < 0 ? "all" : new[] { "first", "middle", "last" }[position];
                _tests.Add(($"Remove {name} {type} tracks and copy survivors", async () =>
                {
                    var setup = await CopySetup(_fixture);
                    var selected = setup.Items.Where(item => item.Info.CodecType == type).ToArray();
                    for (var index = 0; index < selected.Length; index++)
                        selected[index].Remove = position < 0 || index == position;
                    var result = await Convert(_fixture, setup);
                    var expected = _original.Where(stream => stream.Type == type)
                        .Where((_, index) => position >= 0 && index != position).ToArray();
                    var actual = result.Where(stream => stream.Type == type).ToArray();
                    Require(expected.Length == actual.Length, "Wrong remaining track count.");
                    for (var index = 0; index < expected.Length; index++)
                    {
                        Require(expected[index].Language == actual[index].Language, "Wrong remaining language or order.");
                        SamePayload(expected[index], actual[index]);
                    }
                    foreach (var unchanged in _original.Where(stream => stream.Type is "video" or "audio" or "subtitle").Where(stream => stream.Type != type))
                        SamePayload(unchanged, result.Single(stream => stream.Type == unchanged.Type && stream.Language == unchanged.Language));
                }));
            }
        }

        _tests.Add(("All attachments retain bytes, filenames and MIME types", async () =>
        {
            var result = await Convert(_fixture, await CopySetup(_fixture));
            var expected = _original.Where(stream => stream.Type == "attachment").ToArray();
            var actual = result.Where(stream => stream.Type == "attachment").ToArray();
            Require(expected.Length == 2 && actual.Length == expected.Length, "Attachments lost.");
            for (var index = 0; index < expected.Length; index++)
            {
                Require(!string.IsNullOrEmpty(expected[index].ExtraDataHash), "Missing attachment hash.");
                Require(expected[index].ExtraDataHash == actual[index].ExtraDataHash, "Attachment data changed.");
                Require(expected[index].FileName == actual[index].FileName && expected[index].MimeType == actual[index].MimeType,
                    "Attachment metadata changed.");
            }
        }));
        _tests.Add(("Input without subtitles or attachments converts successfully", async () =>
        {
            var result = await Convert(_minimal, await MediaTools.ReadSetup(_minimal));
            Require(result.Length == 2 && result.Single(stream => stream.Type == "audio").Codec == "ac3", "Unexpected minimal conversion result.");
        }));
        _tests.Add(("Existing output is unchanged and does not wait for stdin", async () =>
        {
            var output = Path.Combine(directory, "existing.mkv");
            File.Copy(_minimal, output);
            var hash = SHA256.HashData(await File.ReadAllBytesAsync(output));
            await MediaTools.Run(Utils.CreateFFMpegStartInfo(_fixture, output, await CopySetup(_fixture)));
            var remainingBytes = await File.ReadAllBytesAsync(output);
            Require(hash.SequenceEqual(SHA256.HashData(remainingBytes)), "Existing file was overwritten.");
        }));
        _tests.Add(("Paths with spaces and Cyrillic support probing and conversion", async () =>
        {
            var folder = Path.Combine(directory, "Видео и звук");
            Directory.CreateDirectory(folder);
            var input = Path.Combine(folder, "тестовый файл.mkv");
            var output = Path.Combine(folder, "готовый файл.mkv");
            File.Copy(_fixture, input);
            var setup = await CopySetup(input);
            await MediaTools.Run(Utils.CreateFFMpegStartInfo(input, output, setup));
            var result = await MediaTools.Inspect(output);
            Require(result.Length == _original.Length, "Streams lost with Unicode paths.");
            foreach (var original in _original.Where(stream => stream.Type != "attachment"))
                SamePayload(original, result.Single(stream => stream.Type == original.Type && stream.Language == original.Language));
        }));
    }

    private async Task<MediaStream[]> Convert(string input, Setup setup)
    {
        var output = Path.Combine(directory, $"output-{Guid.NewGuid():N}.mkv");
        await MediaTools.Run(Utils.CreateFFMpegStartInfo(input, output, setup));
        Require(File.Exists(output) && new FileInfo(output).Length > 0, "Output missing or empty.");
        return await MediaTools.Inspect(output);
    }

    private static async Task<Setup> CopySetup(string input)
    {
        var setup = await MediaTools.ReadSetup(input);
        foreach (var item in setup.Items)
            item.Convert = false;
        return setup;
    }

    private static void SamePayload(MediaStream expected, MediaStream actual)
    {
        Require(expected.Codec == actual.Codec, "Copied stream codec changed.");
        Require(expected.PacketHashes.Length > 0, "Fixture has no packets to compare.");
        Require(expected.PacketHashes.SequenceEqual(actual.PacketHashes), "Encoded packet data changed.");
    }

    private static Setup SavedSetup() => new() { Items = Tracks().Select(track => new SetupItem(track)).ToArray() };

    private static ProbeStream[] Tracks() =>
    [
        Track(1, "audio", "eng"), Track(2, "audio", "rus"),
        Track(3, "subtitle", "eng"), Track(4, "subtitle", "rus"),
    ];

    private static ProbeStream Track(int index, string type, string language) => new()
    {
        Index = index,
        CodecType = type,
        CodecName = type == "audio" ? "flac" : "subrip",
        Channels = type == "audio" ? 2 : null,
        Tags = new ProbeTags { Language = language, Title = "Main" },
    };
}
