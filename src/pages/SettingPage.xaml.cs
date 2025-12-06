using ScreenLookup.src.utils;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.pages
{
    public partial class SettingPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool isTessractloading = false;
        private bool isHunspellloading = false;

        public SettingPage()
        {
            DataContext = this;
            InitializeComponent();

            LoadTesseractContent();
            LoadTranslationProvidersContent();
            DownloadTesseractButtonStateChange();
            DownloadHunspellButtonStateChange();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int SourceLanguageAccuracy
        {
            get { return Setting.SourceLanguageAccuracy; }
            set
            {
                Setting.SourceLanguageAccuracy = value;
                DownloadTesseractButtonStateChange();
                OnPropertyChanged();
            }
        }

        public int SourceLanguage
        {
            get { return Setting.SourceLanguage; }
            set
            {
                Setting.SourceLanguage = value;
                DownloadTesseractButtonStateChange();
                DownloadHunspellButtonStateChange();
                OnPropertyChanged();
            }
        }

        public bool HunSpell
        {
            get { return Setting.HunSpell; }
            set
            {
                if (Setting.IsHunspellInstalled(Setting.SourceLanguage))
                    Setting.HunSpell = value;
                else
                {
                    if (value)
                        SnackbarHost.Show($"You have to download Hunspell {LanguageList.GetDisplayNameFromID(Setting.SourceLanguage, true)}", "", "error");

                    Setting.HunSpell = false;
                }

                OnPropertyChanged();
            }
        }

        public int TargetLanguage
        {
            get { return Setting.TargetLanguage; }
            set
            {
                Setting.TargetLanguage = value;
                OnPropertyChanged();
            }
        }

        public int TranslationProvider
        {
            get { return Setting.TranslationProvider; }
            set
            {
                Setting.TranslationProvider = value;
                OnPropertyChanged();
            }
        }

        public bool StartupWithWindows
        {
            get { return Setting.StartupWithWindows; }
            set
            {
                Setting.StartupWithWindows = value;

                if (value)
                    Setting.RegAutorun.SetValue("ScreenLookup", $"\"{AppDomain.CurrentDomain.BaseDirectory}\\ScreenLookup.exe\"");
                else
                    Setting.RegAutorun.DeleteValue("ScreenLookup", false);

                OnPropertyChanged();
            }
        }

        public bool StartInBackground
        {
            get { return Setting.StartInBackground; }
            set
            {
                Setting.StartInBackground = value;
                OnPropertyChanged();
            }
        }
        public bool MinimizeToTray
        {
            get { return Setting.MinimizeToTray; }
            set
            {
                Setting.MinimizeToTray = value;
                OnPropertyChanged();
            }
        }
        public bool ShowImage
        {
            get { return Setting.ShowImage; }
            set
            {
                Setting.ShowImage = value;
                OnPropertyChanged();
            }
        }
        public bool Topmost
        {
            get { return Setting.Topmost; }
            set
            {
                Setting.Topmost = value;
                OnPropertyChanged();
            }
        }
        public int FontSizeS
        {
            get { return Setting.FontSizeS; }
            set
            {
                Setting.FontSizeS = value;
                OnPropertyChanged();
            }
        }

        public string FontFace
        {
            get { return Setting.FontFace; }
            set
            {
                Setting.FontFace = value;
                OnPropertyChanged();
            }
        }

        private void LoadTesseractContent()
        {
            sourceLanguage.Items.Clear();
            targetLanguage.Items.Clear();

            foreach (string languageTesseract in LanguageList.LanguageTesseract)
            {
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);

                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(42)}\t{languageTesseract}";
                sourceLanguage.Items.Add(text);
                targetLanguage.Items.Add(text);
            }
        }

        private void LoadTranslationProvidersContent()
        {
            translationProvider.Items.Clear();

            foreach (string translationProvider in Setting.TranslationProviders)
            {
                this.translationProvider.Items.Add(translationProvider);
            }
        }

        private void DownloadTesseractButtonStateChange()
        {
            downloadTesseract.Visibility = Visibility.Visible;
            if (isTessractloading)
            {
                tesseractLoadingIcon.Visibility = Visibility.Visible;
                tesseractLoadIcon.Visibility = Visibility.Collapsed;
            }
            else if (Setting.IsTesseractInstalled(Setting.SourceLanguageAccuracy, Setting.SourceLanguage))
                downloadTesseract.Visibility = Visibility.Hidden;
            else
            {
                tesseractLoadingIcon.Visibility = Visibility.Collapsed;
                tesseractLoadIcon.Visibility = Visibility.Visible;
            }
        }

        private void DownloadHunspellButtonStateChange()
        {
            downloadHunspell.Visibility = Visibility.Visible;
            if (isHunspellloading)
            {
                hunspellLoadingIcon.Visibility = Visibility.Visible;
                hunspellLoadIcon.Visibility = Visibility.Collapsed;
            }
            else if (Setting.IsHunspellInstalled(Setting.SourceLanguage))
                downloadHunspell.Visibility = Visibility.Hidden;
            else
            {
                HunSpell = false;
                hunspellLoadingIcon.Visibility = Visibility.Collapsed;
                hunspellLoadIcon.Visibility = Visibility.Visible;
            }
        }

        private static async Task MoveFileToFolder(string sourcePath, string destinationPath)
        {
            System.IO.Directory.CreateDirectory(destinationPath);
            FileInfo file = new(sourcePath);
            string filePath = $@"{destinationPath}\{file.Name}";

            if (File.Exists(filePath))
            {
                FileInfo oldFile = new(filePath);
                oldFile.Delete();
            }
            file.MoveTo(filePath);
        }

        private async void DownloadTesseractButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = Setting.SourceLanguage;
            int accID = Setting.SourceLanguageAccuracy;

            string? pickedLanguageFile = LanguageList.LanguageTesseract[langID];
            if (isTessractloading || string.IsNullOrWhiteSpace(pickedLanguageFile))
                return;

            isTessractloading = true;
            DownloadTesseractButtonStateChange();
            SnackbarHost.Show($"Downloading {LanguageList.GetDisplayNameFromID(langID, true)}", "", "info");

            string tesseractFilePath = TesseractHelper.GetTessdataPath();
            string tempFilePath = Path.Combine(Path.GetTempPath(), pickedLanguageFile);

            TesseractGitHubFileDownloader fileDownloader = new();
            await fileDownloader.DownloadFileAsync(pickedLanguageFile, tempFilePath);
            await MoveFileToFolder(tempFilePath, tesseractFilePath);

            isTessractloading = false;
            Setting.SaveTesseractInstalled(accID, langID);
            DownloadTesseractButtonStateChange();
            SnackbarHost.Show($"Download {LanguageList.GetDisplayNameFromID(langID, true)} successfully", "", "success");
        }

        private async void DownloadHunspellButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = Setting.SourceLanguage;
            string DisplayName = LanguageList.GetDisplayNameFromID(langID, false);

            if (!HunspellHelper.FileNames.ContainsKey(DisplayName))
            {
                SnackbarHost.Show($"{LanguageList.GetDisplayNameFromID(langID, true)} dosen't support Hunspell", "", "error");
                return;
            }

            var fileName = HunspellHelper.FileNames[DisplayName];

            isHunspellloading = true;
            DownloadHunspellButtonStateChange();
            SnackbarHost.Show($"Downloading Hunspell {LanguageList.GetDisplayNameFromID(langID, true)}", "", "info");


            //aff
            string tempPath = Path.GetTempPath();
            string nameTag = fileName.Split('.')[1];
            string zipName = fileName.Replace(".", $"\\") + ".aff.zip";
            string zipPath = tempPath + nameTag + ".aff.zip"; ;
            string unzipName = nameTag + ".aff";
            string unzipPath = tempPath + unzipName;

            HunspellFileDownloader fileDownloader = new();
            await fileDownloader.DownloadFileAsync(zipName, zipPath);

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempPath, null, true);
            FileInfo zipFile = new(zipPath);
            zipFile.Delete();
            await MoveFileToFolder(unzipPath, HunspellHelper.FilePath);

            //dic
            tempPath = Path.GetTempPath();
            nameTag = fileName.Split('.')[1];
            zipName = fileName.Replace(".", $"\\") + ".dic.zip";
            zipPath = tempPath + nameTag + ".dic.zip"; ;
            unzipName = nameTag + ".dic";
            unzipPath = tempPath + unzipName;

            await fileDownloader.DownloadFileAsync(zipName, zipPath);

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempPath, null, true);
            zipFile = new(zipPath);
            zipFile.Delete();
            await MoveFileToFolder(unzipPath, HunspellHelper.FilePath);

            isHunspellloading = false;
            Setting.SaveHunspellInstalled(langID);
            DownloadHunspellButtonStateChange();
            SnackbarHost.Show($"Download Hunspell {LanguageList.GetDisplayNameFromID(langID, true)} successfully", "", "success");
        }

        private async void ResetButton(object sender, RoutedEventArgs e)
        {

            bool isYes = await DialogBox.Show("Do you want to reset all setting?", "This operation cannot be undone!", 0);
            if (isYes)
            {
                Setting.Reset();
                await DialogBox.Show("You must to restart this program to apply these changes", "", 1);
            }
        }
    }
}
