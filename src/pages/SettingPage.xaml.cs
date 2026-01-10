using ScreenLookup.src.utils;
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

            LoadTargetLanguageContent();
            LoadSourceAccuracys();
            LoadProvidersContent();

            ApplyStartupWithWindows();

            captureShortcut.KeySet = App.setting.ShortcutKey;

            Loaded += (s, e) =>
            {
                SelectSourceLanguageComboBox();
            };
        }

        private void ApplyStartupWithWindows()
        {
            if (App.setting.StartupWithWindows)
                App.setting.RegAutorun.SetValue("ScreenLookup", $"\"{AppDomain.CurrentDomain.BaseDirectory}\\ScreenLookup.exe\"");
            else
                App.setting.RegAutorun.DeleteValue("ScreenLookup", false);
        }

        private void LoadSourceAccuracys()
        {
            List<string> items = [];

            foreach (string accuracy in App.setting.SourceAccuracys)
            {
                items.Add(accuracy);
            }

            sourceLanguageAccuracy.ItemsSource = items;
        }

        private void LoadTargetLanguageContent()
        {
            List<string> items = [];
            for (int langID = 0; langID < TesseractHelper.LangList.Length - 1; langID++)
            {
                string tesseractTag = TesseractHelper.LangList[langID];
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{tesseractTag}";

                items.Add(text);
            }
            targetLanguage.ItemsSource = items;
        }

        private void LoadSourceLanguageContent()
        {
            int langAcc = App.setting.SourceLanguageAccuracy;
            List<ComboBoxItem> items = [];

            for (int langID = 0; langID < TesseractHelper.LangList.Length - 1; langID++)
            {
                string tesseractTag = TesseractHelper.LangList[langID];
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{tesseractTag}";
                bool isInstalled = TesseractHelper.IsInstalled(langAcc, langID);

                items.Add(new ComboBoxItem
                {
                    Content = $"{text}",
                    Tag = langID,
                    FontWeight = isInstalled ? FontWeights.ExtraBold : FontWeights.Normal,
                    Uid = (!isInstalled).ToString(),
                });
            }

            // sourceLanguage downloaded at top
            items = items.OrderBy(o => o.Uid).ToList();
            sourceLanguage.ItemsSource = items;

            SelectSourceLanguageComboBox();
        }

        private void LoadProvidersContent()
        {
            List<string> items = [];

            foreach (string provider in App.setting.ProviderServices)
            {
                items.Add(provider);
            }

            this.translationProvider.ItemsSource = items;
            this.ttsProvider.ItemsSource = items;
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

            if (isLoadingTesseract || string.IsNullOrWhiteSpace(TesseractHelper.LangList[langID]))
                return;

            string pickedLanguageFile = $"{TesseractHelper.LangList[langID]}.traineddata";

            isLoadingTesseract = true;
            ButtonDownloadTesseracChanged();
            SnackbarHost.Show("Source Language", $"Downloading {App.setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}...", "info", 99999, closeButton: false);

            string tesseractFilePath = TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy);
            string filePath = Path.Combine(App.tempFolder, pickedLanguageFile);

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
            LoadSourceLanguageContent();
        }

        private async void DownloadHunspellButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = App.setting.SourceLanguage;
            string tessTag = LanguageList.GetTesseractTagFromID(langID);

            if (!HunspellHelper.LangList.TryGetValue(tessTag, out string? fileName))
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
                string nameTag = fileName.Split('/')[1];
                //string zipPath = $"{App.teampFolder}{nameTag}.{extension}.zip";
                string zipPath = Path.Combine(App.tempFolder, $"{nameTag}.{extension}.zip");

                DownloadHelper fileDownloader = new();
                bool isDownloaded = await fileDownloader.DownloadFileAsync($"https://translator.gres.biz/resources/dictionaries/{fileName}.{extension}.zip", zipPath);

                if (isDownloaded)
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, App.tempFolder, null, true);
                    FileInfo zipFile = new(zipPath);
                    zipFile.Delete();

                    await DownloadHelper.MoveFileToFolder(Path.Combine(App.tempFolder, $"{nameTag}.{extension}"), HunspellHelper.FilePath);
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

            bool isYes = await DialogBox.Show("Do you want to reset all setting?", "This resets all settings and also deletes downloaded language files!", 0);
            if (isYes)
            {
                App.setting.Reset();
                DownloadHelper.DeleteDownloadedAppData();

                await DialogBox.Show("You must to restart this program to apply these changes", "", 1);
            }
        }
        private void SourceLanguageAccuracy_Changed(object sender, SelectionChangedEventArgs e)
        {
            ButtonDownloadTesseracChanged();
            LoadSourceLanguageContent();
        }

        private void SourceLanguage_Changed(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedItem = comboBox.SelectedItem as ComboBoxItem;

            if (selectedItem != null)
                App.setting.SourceLanguage = Int32.Parse(selectedItem.Tag.ToString());

            ButtonDownloadTesseracChanged();
            ButtonDownloadHunspellChanged();
        }

        public void SelectSourceLanguageComboBox()
        {
            foreach (ComboBoxItem item in sourceLanguage.Items)
            {
                if (Int32.Parse(item.Tag.ToString()) == App.setting.SourceLanguage)
                {
                    sourceLanguage.SelectedItem = item;
                    break;
                }
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
