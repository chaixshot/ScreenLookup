using ScreenLookup.src.utils;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for SettingPage.xaml - Application settings, language model management, and provider configuration.
    /// </summary>
    public partial class SettingPage : Page
    {
        #region Fields
        private bool isLoadingTesseract = false;
        private bool isLoadingHunspell = false;
        #endregion

        #region Constructor & Lifecycle
        public SettingPage()
        {
            DataContext = App.setting;
            InitializeComponent();

            LoadSourceAccuracyContent();
            LoadSourceLanguageContent();
            LoadTargetLanguageContent();
            LoadProvidersContent();

            ButtonDownloadTesseractChanged();
            ButtonDownloadHunspellChanged();

            captureShortcut.KeySet = App.setting.ShortcutKey;

            Loaded += (s, e) =>
            {
                SelectSourceLanguage();
            };

            App.settingPage = this;
        }
        #endregion

        #region Content Loading Methods
        private void LoadSourceAccuracyContent()
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

        public void LoadSourceLanguageContent()
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
                    Content = text,
                    Tag = langID,
                    FontWeight = isInstalled ? FontWeights.ExtraBold : FontWeights.Normal,
                    Uid = (!isInstalled).ToString(),
                });
            }

            items = items.OrderBy(o => o.Uid).ToList();
            sourceLanguage.ItemsSource = items;

            SelectSourceLanguage();
        }

        private void LoadProvidersContent()
        {
            List<string> items = [];
            foreach (string provider in App.setting.ProviderServices)
            {
                items.Add(provider);
            }

            translationProvider.ItemsSource = items;
            ttsProvider.ItemsSource = items;
        }

        private void ButtonDownloadTesseractChanged()
        {
            downloadTesseract.IsEnabled = true;
            downloadTesseract.Visibility = Visibility.Visible;
            if (isLoadingTesseract)
            {
                downloadTesseract.IsEnabled = false;
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
            downloadHunspell.IsEnabled = true;
            downloadHunspell.Visibility = Visibility.Visible;
            if (isLoadingHunspell)
            {
                downloadHunspell.IsEnabled = false;
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

        public void SelectSourceLanguage()
        {
            foreach (ComboBoxItem item in sourceLanguage.Items)
            {
                if (int.Parse(item.Tag.ToString() ?? "0") == App.setting.SourceLanguage)
                {
                    sourceLanguage.SelectedItem = item;
                    break;
                }
            }
        }
        #endregion

        #region Action Handlers
        private async void DownloadTesseractButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = App.setting.SourceLanguage;
            int accID = App.setting.SourceLanguageAccuracy;

            if (isLoadingTesseract || string.IsNullOrWhiteSpace(TesseractHelper.LangList[langID]))
                return;

            string pickedLanguageFile = $"{TesseractHelper.LangList[langID]}.traineddata";

            isLoadingTesseract = true;
            ButtonDownloadTesseractChanged();
            SnackbarHost.Show("Source Language", $"Downloading {App.setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}...", SnackbarType.Info, timeout: 99999, closeButton: false);

            string tesseractFilePath = TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy);
            string filePath = Path.Combine(App.tempFolder, pickedLanguageFile);
            bool isFileExist = File.Exists(Path.Combine(tesseractFilePath, pickedLanguageFile));
            bool isDownloaded = false;

            if (!isFileExist)
            {
                DownloadHelper fileDownloader = new();
                isDownloaded = await fileDownloader.DownloadFileAsync($"https://raw.githubusercontent.com/tesseract-ocr/{(accID == 0 ? "tessdata" : accID == 1 ? "tessdata_best" : "tessdata_fast")}/main/{pickedLanguageFile}", filePath);
                await DownloadHelper.MoveFileToFolder(filePath, tesseractFilePath);
            }

            if (isDownloaded || isFileExist)
            {
                TesseractHelper.SaveInstalled(accID, langID);
                SnackbarHost.Show("Source Language", $"\"{App.setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", SnackbarType.Success);
            }
            else
                SnackbarHost.Show("Source Language", $"Unable to download \"{App.setting.SourceAccuracys[accID]} - {LanguageList.GetDisplayNameFromID(langID, true)}\"", SnackbarType.Error);

            isLoadingTesseract = false;
            ButtonDownloadTesseractChanged();
            LoadSourceLanguageContent();
        }

        private async void DownloadHunspellButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = App.setting.SourceLanguage;
            string tessTag = LanguageList.GetTesseractTagFromID(langID);

            if (!HunspellHelper.LangList.TryGetValue(tessTag, out string? fileName))
            {
                SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(langID, true)}\" doesn't support Hunspell", SnackbarType.Error);
                return;
            }

            isLoadingHunspell = true;
            ButtonDownloadHunspellChanged();
            SnackbarHost.Show("Hunspell", $"Downloading Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}...", SnackbarType.Info, timeout: 99999, closeButton: false);

            foreach (string extension in new[] { "aff", "dic" })
            {
                string nameTag = fileName.Split('/')[1];
                string zipPath = Path.Combine(App.tempFolder, $"{nameTag}.{extension}.zip");
                bool isFileExist = File.Exists(Path.Combine(HunspellHelper.FilePath, $"{nameTag}.{extension}"));
                bool isDownloaded = false;

                if (!isFileExist)
                {
                    DownloadHelper fileDownloader = new();
                    isDownloaded = await fileDownloader.DownloadFileAsync($"https://translator.gres.biz/resources/dictionaries/{fileName}.{extension}.zip", zipPath);
                }

                if (isDownloaded)
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, App.tempFolder, null, true);
                    FileInfo zipFile = new(zipPath);
                    zipFile.Delete();

                    await DownloadHelper.MoveFileToFolder(Path.Combine(App.tempFolder, $"{nameTag}.{extension}"), HunspellHelper.FilePath);
                }
                else if (!isFileExist)
                {
                    SnackbarHost.Show("Hunspell", $"Unable to download \"Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}\"", SnackbarType.Error);
                    isLoadingHunspell = false;
                    ButtonDownloadHunspellChanged();
                    return;
                }
            }

            isLoadingHunspell = false;
            HunspellHelper.SaveInstalled(langID);
            ButtonDownloadHunspellChanged();
            SnackbarHost.Show("Hunspell", $"\"Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", SnackbarType.Success);
        }

        private void HunSpell_Click(object sender, RoutedEventArgs e)
        {
            if (!HunspellHelper.IsInstalled(App.setting.SourceLanguage))
                SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", SnackbarType.Error);
        }

        private async void Reset__Click(object sender, RoutedEventArgs e)
        {
            bool isYes = await DialogBox.Show("Do you want to reset all settings?", "This resets all settings and also deletes downloaded language files!", "Yes", "No");
            if (isYes)
            {
                Settings.Reset();
                DownloadHelper.DeleteDownloadedAppData();

                await DialogBox.Show("You must restart the program to apply these changes", string.Empty, string.Empty, "OK");
            }
        }
        #endregion

        #region ComboBox & Control Handlers
        private void SourceLanguageAccuracy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
                ButtonDownloadTesseractChanged();
        }

        private void SourceLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                    App.setting.SourceLanguage = int.Parse(selectedItem.Tag.ToString() ?? "0");

                ButtonDownloadTesseractChanged();
                ButtonDownloadHunspellChanged();
            }
        }

        private void ShortcutControl_KeySetChanged(object sender, EventArgs e)
        {
            if (IsLoaded)
                App.setting.ShortcutKey = captureShortcut.KeySet;
        }
        #endregion
    }
}
