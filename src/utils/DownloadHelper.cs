namespace ScreenLookup.src.utils
{
    internal class DownloadHelper
    {
        private readonly HttpClient _client;

        public DownloadHelper()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "ScreenLookup tesseract language downloader");
        }

        public static async Task MoveFileToFolder(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);
            FileInfo file = new(sourcePath);
            string filePath = Path.Combine(destinationPath, file.Name);

            if (File.Exists(filePath))
            {
                FileInfo oldFile = new(filePath);
                oldFile.Delete();
            }
            file.MoveTo(filePath);
            await Task.CompletedTask;
        }

        public async Task<bool> DownloadFileAsync(string fileUrl, string localDestination)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(fileUrl);
                response.EnsureSuccessStatusCode();

                byte[] fileContents = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localDestination, fileContents);
                return true;
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("DownloadHelper", $"Error downloading file: {ex.Message}", type: SnackbarType.Error);
                return false;
            }
        }

        public static void DeleteDownloadedAppData()
        {
            foreach (string folderName in new[] { "hunspell", "tessdata", "tessdata_best", "tessdata_fast" })
            {
                DirectoryInfo folder = new(Path.Combine(App.appDataFolder, folderName));
                if (folder.Exists)
                    folder.Delete(true);
            }
        }

        public static bool IsInstalled(int accID, int langID)
        {
            return App.setting.OcrEngineMode switch
            {
                OcrHelper.OcrEngine.TesseraceOCR => TesseractHelper.IsInstalled(accID, langID),
                OcrHelper.OcrEngine.PaddleOCR => PaddleHelper.IsInstalled(accID),
                _ => TesseractHelper.IsInstalled(accID, langID),
            };
        }
    }
}
