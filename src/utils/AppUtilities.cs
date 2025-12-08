using System.Diagnostics;
using Windows.ApplicationModel;

namespace ScreenLookup.src.utils
{
    internal class AppUtilities
    {
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
    }
}
