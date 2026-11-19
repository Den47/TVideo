# TVideo

TVideo is a Windows app for editing the audio and subtitle tracks in MKV files. You can convert audio to AC-3, drop languages you don’t need, and save a new MKV. TVideo copies the first video track without re-encoding it.

## Edit an MKV

1. Drop one or more MKV files onto the window and choose where to save the results.
2. In **Setup**, click the red **×** beside any audio or subtitle track you want to remove. For each audio track you keep, turn **AC-3** on to convert it or off to keep its original format.
3. Click **Run**. TVideo writes the new files to your chosen folder and gives them unique names if needed.

For a batch of episodes, select **Apply to all** before clicking **Run**. TVideo reuses your choices while the tracks match and asks again if an episode has a different layout. To check a file’s audio and subtitle tracks before converting it, click **Get Info**.

## Run from source

Install the .NET 10 SDK on Windows. From the repository root, run the app in the `src` folder so it can find the bundled FFmpeg tools.

```powershell
cd src
dotnet run --project TVideo.csproj
```

See the [test guide](tests/TVideo.Tests/README.md) to run the regression tests.
