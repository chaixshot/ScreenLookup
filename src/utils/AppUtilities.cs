using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;

namespace ScreenLookup.src.utils
{
    internal class AppUtilities
    {

        public const string GitHubRepoUrl = "https://github.com/chaixshot/ScreenLookup";
        public const string GitHubReleasesUrl = "https://github.com/chaixshot/ScreenLookup/releases";
        public const string GitHubLatestReleaseApi = "https://api.github.com/repos/chaixshot/ScreenLookup/releases/latest";

        private static SoundPlayer soundPlayer;

        internal static bool IsPackaged()
        {
            try
            {
                // If we have a package ID then we are running in a packaged context
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
                return $"{version.Major}.{version.Minor}.{version.Build}" ?? "unknown error reading package version";
            }

            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown error reading assembly version";
        }

        internal static async Task<string> GetLatestVersionAsync()
        {
            using HttpClient client = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenLookup");
            string response = await client.GetStringAsync(GitHubLatestReleaseApi);
            using var doc = JsonDocument.Parse(response);
            string latestVersionRaw = doc.RootElement.GetProperty("tag_name").GetString();
            string latestVersion = string.IsNullOrEmpty(latestVersionRaw)
                ? String.Empty
                : Regex.Replace(latestVersionRaw, @"[^0-9.]", string.Empty);

            return latestVersion;
        }

        // Open explorer and select file
        internal static void OpenExplorer(string filePath)
        {
            string args = string.Format("/e, /select, \"{0}\"", filePath);
            ProcessStartInfo info = new()
            {
                FileName = "explorer",
                Arguments = args
            };
            Process.Start(info);
        }

        internal static void PlaySound(string soundName)
        {
            string soundPath = Path.Combine(Environment.CurrentDirectory, "src\\sounds", soundName);

            // Verify the file exists before attempting to play it
            if (File.Exists(soundPath))
            {
                soundPlayer ??= new(soundPath);
                soundPlayer.SoundLocation = soundPath;
                soundPlayer.Play();
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
    }
}
