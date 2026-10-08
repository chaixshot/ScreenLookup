using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;

namespace ScreenLookup.src.utils
{
    internal partial class AppUtilities
    {
        #region Constants & Regex
        public const string GitHubRepoUrl = "https://github.com/chaixshot/ScreenLookup";
        public const string GitHubReleasesUrl = "https://github.com/chaixshot/ScreenLookup/releases";
        public const string GitHubLatestReleaseApi = "https://api.github.com/repos/chaixshot/ScreenLookup/releases/latest";

        [GeneratedRegex(@"\s*([.!?,。！？，、:;{}\[\]()'‘’""])\s*")]
        internal static partial Regex RegexPunctuation();

        [GeneratedRegex(@"\s*([{}\[\]])\s*")]
        internal static partial Regex RegexBracket();

        [GeneratedRegex(@"(?<=[.!?。！？，、;{}\[\]()])")]
        internal static partial Regex PunctuationBoundary();
        #endregion

        #region Application Info
        internal static bool IsPackaged()
        {
            try
            {
                PackageId dummy = Package.Current.Id;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static string GetAppVersion()
        {
            if (IsPackaged())
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        }

        internal static async Task<string> GetLatestVersionAsync()
        {
            using HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenLookup");
            string response = await client.GetStringAsync(GitHubLatestReleaseApi);
            using var doc = JsonDocument.Parse(response);
            string? latestVersionRaw = doc.RootElement.GetProperty("tag_name").GetString();
            string latestVersion = string.IsNullOrEmpty(latestVersionRaw)
                ? string.Empty
                : Regex.Replace(latestVersionRaw, @"[^0-9.]", string.Empty);

            return latestVersion;
        }

        // Open explorer and select file
        #endregion

        #region System Helpers
        internal static void OpenExplorer(string filePath)
        {
            string args = $"/e, /select, \"{filePath}\"";
            ProcessStartInfo info = new()
            {
                FileName = "explorer",
                Arguments = args
            };
            Process.Start(info);
        }

        internal static void PlaySound(string soundName)
        {
            string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "sounds", soundName);
            if (!File.Exists(soundPath))
            {
                soundPath = Path.Combine(Environment.CurrentDirectory, "src", "sounds", soundName);
            }

            if (File.Exists(soundPath))
            {
                Task.Run(() =>
                {
                    try
                    {
                        using var audioFile = new AudioFileReader(soundPath);
                        using var outputDevice = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);

                        outputDevice.Init(audioFile);
                        outputDevice.Play();

                        while (outputDevice.PlaybackState == PlaybackState.Playing)
                        {
                            Thread.Sleep(20);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PlaySound] NAudio playback error: {ex.Message}");
                        try
                        {
                            using var player = new SoundPlayer(soundPath);
                            player.PlaySync();
                        }
                        catch (Exception fallbackEx)
                        {
                            Debug.WriteLine($"[PlaySound] Fallback SoundPlayer error: {fallbackEx.Message}");
                        }
                    }
                });
            }
        }

        internal static async void ChackForUpdate()
        {
            string currentVersion = GetAppVersion();
            string latestVersion = string.Empty;

            try
            {
                latestVersion = await GetLatestVersionAsync();
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("Error", $"Update Check Failed:\n\"{ex.Message}\"", type: SnackbarType.Error);
                return;
            }

            if (!string.IsNullOrEmpty(latestVersion) && latestVersion != currentVersion)
            {
                bool isYes = await DialogBox.Show("New Version Available",
                $"A new version has been detected: {latestVersion}\n" +
                $"Current version: {currentVersion}\n" +
                $"Please visit GitHub to download the latest release.",
                "Update", "Dismiss");

                if (isYes)
                {
                    string url = GitHubReleasesUrl;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        SnackbarHost.Show("Error", $"Open Browser Failed:\n\"{ex.Message}\"", type: SnackbarType.Error);
                    }
                }
            }
        }

        internal static bool IsCjk(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) || // CJK Unified Ideographs
                   (c >= 0x3040 && c <= 0x309F) || // Hiragana
                   (c >= 0x30A0 && c <= 0x30FF) || // Katakana
                   (c >= 0x3400 && c <= 0x4DBF) || // CJK Extension A
                   (c >= 0xAC00 && c <= 0xD7AF);   // Hangul Syllables
        }
        #endregion
    }
}
