using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Diagnostics;
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
            LoadProvidersContent();

            ButtonDownloadTesseracChanged();
            ButtonDownloadHunspellChanged();

            ApplyStartupWithWindows();

            captureShortcut.KeySet = Setting.ShortcutKey;
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

        public int TTSProvider
        {
            get { return Setting.TTSProvider; }
            set
            {
                Setting.TTSProvider = value;
                OnPropertyChanged();
            }
        }

        public bool StartupWithWindows
        {
            get { return Setting.StartupWithWindows; }
            set
            {
                Setting.StartupWithWindows = value;

                ApplyStartupWithWindows();
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

        public bool ShowHighlight
        {
            get { return Setting.ShowHighlight; }
            set
            {
                Setting.ShowHighlight = value;
                OnPropertyChanged();
            }
        }

        public bool CloseLostFocus
        {
            get { return Setting.CloseLostFocus; }
            set
            {
                Setting.CloseLostFocus = value;
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

        private void ApplyStartupWithWindows()
        {
            if (Setting.StartupWithWindows)
                Setting.RegAutorun.SetValue("ScreenLookup", $"\"{AppDomain.CurrentDomain.BaseDirectory}\\ScreenLookup.exe\"");
            else
                Setting.RegAutorun.DeleteValue("ScreenLookup", false);
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

        private void LoadProvidersContent()
        {
            translationProvider.Items.Clear();
            ttsProvider.Items.Clear();

            foreach (string provider in Setting.ProviderServices)
            {
                this.translationProvider.Items.Add(provider);
                this.ttsProvider.Items.Add(provider);
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
            SnackbarHost.Show("Source Language", $"Downloading {Setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}...", "info", 99999, closeButton: false);

            string tesseractFilePath = TesseractHelper.GetTessdataPath();
            string tempPath = Path.Combine(Path.GetTempPath(), "ScreenLookup");
            string filePath = Path.Combine(tempPath, pickedLanguageFile);

            DownloadHelper fileDownloader = new();
            bool isDownloaded = await fileDownloader.DownloadFileAsync($"https://raw.githubusercontent.com/tesseract-ocr/{(accID == 0 ? "tessdata" : accID == 1 ? "tessdata_best" : "tessdata_fast")}/main/{pickedLanguageFile}", filePath);

            if (isDownloaded)
            {
                await DownloadHelper.MoveFileToFolder(filePath, tesseractFilePath);
                Setting.SaveTesseractInstalled(accID, langID);
                SnackbarHost.Show("Source Language", $"\"{Setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", "success");
            }
            else
                SnackbarHost.Show("Source Language", $"Unable to download \"{Setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}\"", "error");

            isLoadingTesseract = false;
            ButtonDownloadTesseracChanged();
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
            SnackbarHost.Show("Hunspell", $"Downloading Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}...", "info", 99999, closeButton: false);

            // Download files
            foreach (string extension in new string[] { "aff", "dic" })
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "ScreenLookup");
                string nameTag = fileName.Split('/')[1];
                //string zipPath = $"{tempPath}{nameTag}.{extension}.zip";
                string zipPath = Path.Combine(tempPath, $"{nameTag}.{extension}.zip");

                DownloadHelper fileDownloader = new();
                bool isDownloaded = await fileDownloader.DownloadFileAsync($"https://translator.gres.biz/resources/dictionaries/{fileName}.{extension}.zip", zipPath);

                if (isDownloaded)
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempPath, null, true);
                    FileInfo zipFile = new(zipPath);
                    zipFile.Delete();

                    await DownloadHelper.MoveFileToFolder(Path.Combine(tempPath, $"{nameTag}.{extension}"), HunspellHelper.FilePath);
                }
                else
                {
                    SnackbarHost.Show("Hunspell", $"Unable to download \"Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}\"", "error");

                    isLoadingHunspell = false;
                    ButtonDownloadHunspellChanged();
                    return;
                }

            }

            isLoadingHunspell = false;
            Setting.SaveHunspellInstalled(langID);
            ButtonDownloadHunspellChanged();
            SnackbarHost.Show("Hunspell", $"\"Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", "success");
        }

        private void ShortcutControl_KeySetChanged(object sender, EventArgs e)
        {
            Setting.ShortcutKey = captureShortcut.KeySet;
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

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
