using ScreenLookup.src.utils;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.pages
{
    public partial class SettingPage : Page
    {
        private bool isLoadingTesseract = false;
        private bool isLoadingHunspell = false;

        public SettingPage()
        {
            DataContext = App.setting;
            InitializeComponent();

            LoadTesseractContent();
            LoadProvidersContent();

            ButtonDownloadHunspellChanged();
            ButtonDownloadTesseracChanged();

            ApplyStartupWithWindows();

            captureShortcut.KeySet = App.setting.ShortcutKey;
        }

        private void ApplyStartupWithWindows()
        {
            if (App.setting.StartupWithWindows)
                App.setting.RegAutorun.SetValue("ScreenLookup", $"\"{AppDomain.CurrentDomain.BaseDirectory}\\ScreenLookup.exe\"");
            else
                App.setting.RegAutorun.DeleteValue("ScreenLookup", false);
        }

        private void LoadTesseractContent()
        {
            sourceLanguageAccuracy.Items.Clear();
            sourceLanguage.Items.Clear();
            targetLanguage.Items.Clear();

            for (int langID = 0; langID < LanguageList.LanguageTesseract.Length - 1; langID++)
            {
                string languageTesseract = LanguageList.LanguageTesseract[langID];
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{languageTesseract}";

                ComboBoxItem item = new()
                {
                    Content = $"{text}",
                };

                if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, langID))
                    item.FontWeight = FontWeights.ExtraBold;

                sourceLanguage.Items.Add(item);
                targetLanguage.Items.Add(text);
            }

            foreach (string accuracy in App.setting.SourceAccuracys)
            {
                sourceLanguageAccuracy.Items.Add(accuracy);
            }
        }

        private void LoadProvidersContent()
        {
            translationProvider.Items.Clear();
            ttsProvider.Items.Clear();

            foreach (string provider in App.setting.ProviderServices)
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
            else if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
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
            else if (HunspellHelper.IsInstalled(App.setting.SourceLanguage))
                downloadHunspell.Visibility = Visibility.Hidden;
            else
            {
                hunspell.IsChecked = false;
                hunspellLoadingIcon.Visibility = Visibility.Collapsed;
                hunspellLoadIcon.Visibility = Visibility.Visible;
            }
        }

        private async void DownloadTesseractButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = App.setting.SourceLanguage;
            int accID = App.setting.SourceLanguageAccuracy;

            string? pickedLanguageFile = LanguageList.LanguageTesseract[langID];
            if (isLoadingTesseract || string.IsNullOrWhiteSpace(pickedLanguageFile))
                return;

            isLoadingTesseract = true;
            ButtonDownloadTesseracChanged();
            SnackbarHost.Show("Source Language", $"Downloading {App.setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}...", "info", 99999, closeButton: false);

            string tesseractFilePath = TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy);
            string tempPath = Path.Combine(Path.GetTempPath(), "ScreenLookup");
            string filePath = Path.Combine(tempPath, pickedLanguageFile);

            DownloadHelper fileDownloader = new();
            bool isDownloaded = await fileDownloader.DownloadFileAsync($"https://raw.githubusercontent.com/tesseract-ocr/{(accID == 0 ? "tessdata" : accID == 1 ? "tessdata_best" : "tessdata_fast")}/main/{pickedLanguageFile}", filePath);

            if (isDownloaded)
            {
                await DownloadHelper.MoveFileToFolder(filePath, tesseractFilePath);
                TesseractHelper.SaveInstalled(accID, langID);
                SnackbarHost.Show("Source Language", $"\"{App.setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", "success");
            }
            else
                SnackbarHost.Show("Source Language", $"Unable to download \"{App.setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}\"", "error");

            isLoadingTesseract = false;
            ButtonDownloadTesseracChanged();
            LoadTesseractContent();
        }

        private async void DownloadHunspellButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = App.setting.SourceLanguage;
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
            HunspellHelper.SaveInstalled(langID);
            ButtonDownloadHunspellChanged();
            SnackbarHost.Show("Hunspell", $"\"Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", "success");
        }

        private void ShortcutControl_KeySetChanged(object sender, EventArgs e)
        {
            App.setting.ShortcutKey = captureShortcut.KeySet;
        }

        private async void ResetButton(object sender, RoutedEventArgs e)
        {

            bool isYes = await DialogBox.Show("Do you want to reset all setting?", "This operation cannot be undone!", 0);
            if (isYes)
            {
                App.setting.Reset();
                await DialogBox.Show("You must to restart this program to apply these changes", "", 1);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void SourceLanguageAccuracy_Changed(object sender, SelectionChangedEventArgs e)
        {
            ButtonDownloadTesseracChanged();
        }

        private void SourceLanguage_Changed(object sender, SelectionChangedEventArgs e)
        {
            ButtonDownloadTesseracChanged();
            ButtonDownloadHunspellChanged();
        }

        private void HunSpell_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox checkbox = sender as CheckBox;
            bool isCheck = (bool)checkbox.IsChecked;

            if (HunspellHelper.IsInstalled(App.setting.SourceLanguage))
                hunspell.IsChecked = isCheck;
            else
            {
                if (isCheck)
                    SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", "error");

                hunspell.IsChecked = false;
            }
        }

        private void HunSpell_Click(object sender, RoutedEventArgs e)
        {
            if (!HunspellHelper.IsInstalled(App.setting.SourceLanguage))
                SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", "error");
        }

        private void StartupWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            ApplyStartupWithWindows();
        }
    }
}
