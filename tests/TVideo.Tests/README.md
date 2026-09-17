# TVideo regression tests

This standalone Windows console test assembly uses the real TVideo assembly and the bundled FFmpeg/ffprobe executables. No additional NuGet packages or downloaded media are needed. It does not open the WPF interface.

Run all tests from the repository root:

```powershell
dotnet run --project tests/TVideo.Tests/TVideo.Tests.csproj --configuration Release
```

Build the application and tests together:

```powershell
dotnet build TVideo.slnx --configuration Release
```

The runner prints each result and returns exit code 1 if any test, fixture creation, or cleanup fails. Use `dotnet run`, not `dotnet test`: this project has a dependency-free console runner rather than a test framework adapter.

## Coverage

- Track compatibility: matching tracks, removed tracks, added/missing audio and subtitles, reordered languages, changed codecs/channels/languages/titles, missing tags, normalized metadata, and absolute stream index shifts.
- Available output names, numbered suffixes, occupied files/directories, and the default destination.
- Actual AC3 conversion and mixed copying/conversion after removing an audio track.
- Removal of first, middle, last, and all audio/subtitle tracks, checking surviving languages and encoded packet hashes.
- Preservation of video packets and attachment bytes, filenames, and MIME types.
- Inputs without subtitles or attachments, existing output protection with stdin kept open, and spaces/Cyrillic in paths.
- Temporary directory cleanup on an intentional exception.

## Fixtures and cleanup

FFmpeg creates one 16x16 video frame (40 ms), 40 ms of audio per track, and single-cue subtitles. AC3 frame boundaries may pad the converted audio slightly. Fixtures are generated once per run and reused as read-only inputs. The attachment fixture includes a tiny text file and a synthetic font-shaped payload with a font filename/MIME type; it tests opaque attachment preservation, not font rendering.

Each run owns a unique directory under `%TEMP%/TVideo.Tests`. All generated inputs, outputs, subtitles, and attachments are deleted on normal completion and managed exceptions, even when tests fail. Processes have a 15-second timeout; a timed-out process tree is killed and awaited before cleanup. Forced termination of the runner or a power failure can prevent cleanup.

Assertions examine the output with ffprobe and compare encoded packet SHA-256 hashes for copied streams and extradata hashes for attachments. They do not rely solely on process exit codes: the bundled FFmpeg can return 0 when `-n` refuses an existing output.

Compatibility is tested through `Setup.IsCompatibleWith`; displaying the settings dialog on a mismatch remains a UI behavior, outside this headless suite.
