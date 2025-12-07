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

        private bool isLoadingTesseract = false;
        private bool isLoadingHunspell = false;

        public SettingPage()
        {
            DataContext = this;
            InitializeComponent();

            LoadTesseractContent();
            LoadTranslationProvidersContent();

            ButtonDownloadTesseracChanged();
            ButtonDownloadHunspellChanged();
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
                ButtonDownloadTesseracChanged();
                OnPropertyChanged();
            }
        }

        public int SourceLanguage
        {
            get { return Setting.SourceLanguage; }
            set
            {
                Setting.SourceLanguage = value;
                ButtonDownloadTesseracChanged();
                ButtonDownloadHunspellChanged();
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
                        SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(Setting.SourceLanguage, true)}\"", "error");

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
            sourceLanguageAccuracy.Items.Clear();
            sourceLanguage.Items.Clear();
            targetLanguage.Items.Clear();

            foreach (string languageTesseract in LanguageList.LanguageTesseract)
            {
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);

                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(42)}\t{languageTesseract}";
                sourceLanguage.Items.Add(text);
                targetLanguage.Items.Add(text);
            }

            foreach (string accuracy in Setting.SourceAccuracys)
            {
                sourceLanguageAccuracy.Items.Add(accuracy);
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

        private void ButtonDownloadTesseracChanged()
        {
            downloadTesseract.Visibility = Visibility.Visible;
            if (isLoadingTesseract)
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

        private void ButtonDownloadHunspellChanged()
        {
            downloadHunspell.Visibility = Visibility.Visible;
            if (isLoadingHunspell)
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

        private async void DownloadTesseractButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = Setting.SourceLanguage;
            int accID = Setting.SourceLanguageAccuracy;

            string? pickedLanguageFile = LanguageList.LanguageTesseract[langID];
            if (isLoadingTesseract || string.IsNullOrWhiteSpace(pickedLanguageFile))
                return;

            isLoadingTesseract = true;
            ButtonDownloadTesseracChanged();
            SnackbarHost.Show("Source Language", $"Downloading {Setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}...", "info", 1000);

            string tesseractFilePath = TesseractHelper.GetTessdataPath();
            string tempFilePath = Path.Combine(Path.GetTempPath(), pickedLanguageFile);

            TesseractDownloader fileDownloader = new();
            await fileDownloader.DownloadFileAsync(pickedLanguageFile, tempFilePath);
            await DownloadHelper.MoveFileToFolder(tempFilePath, tesseractFilePath);

            isLoadingTesseract = false;
            Setting.SaveTesseractInstalled(accID, langID);
            ButtonDownloadTesseracChanged();
            SnackbarHost.Show("Source Language", $"\"{Setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", "success");
        }

        private async void DownloadHunspellButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = Setting.SourceLanguage;
            string DisplayName = LanguageList.GetDisplayNameFromID(langID, false);

            if (!HunspellHelper.FileNames.TryGetValue(DisplayName, out string? fileName))
            {
                SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(langID, true)}\" dosen't support Hunspell", "error");
                return;
            }

            isLoadingHunspell = true;
            ButtonDownloadHunspellChanged();
            SnackbarHost.Show("Hunspell", $"Downloading Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}...", "info", 1000);

            // Download files
            foreach (string extension in new string[] { "aff", "dic" })
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "ScreenLookup");
                string nameTag = fileName.Split('/')[1];
                string zipPath = $"{tempPath}{nameTag}.{extension}.zip";

                HunspellDownloader fileDownloader = new();
                await fileDownloader.DownloadFileAsync($"{fileName}.{extension}.zip", zipPath);

                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempPath, null, true);
                FileInfo zipFile = new(zipPath);
                zipFile.Delete();

                await DownloadHelper.MoveFileToFolder(Path.Combine(tempPath, $"{nameTag}.{extension}"), HunspellHelper.FilePath);
            }

            isLoadingHunspell = false;
            Setting.SaveHunspellInstalled(langID);
            ButtonDownloadHunspellChanged();
            SnackbarHost.Show("Hunspell", $"\"Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", "success");
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
