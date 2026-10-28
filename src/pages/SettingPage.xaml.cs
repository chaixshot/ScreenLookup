using ScreenLookup.src.llmAPI;
using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;
using static ScreenLookup.src.utils.OcrHelper;

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

            LoadOcrEngineModes();
            LoadSourceAccuracyContent();
            LoadSourceLanguageContent();
            LoadTargetLanguageContent();
            LoadProvidersContent();
            UpdateApiSettingsVisibility();

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
        private void LoadOcrEngineModes()
        {
            ocrEngineMode.ItemsSource = Enum.GetValues<OcrEngine>();
        }

        private void LoadSourceAccuracyContent()
        {
            sourceLanguageAccuracy.ItemsSource = Enum.GetValues<SourceAccuracy>();
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
            SourceAccuracy langAcc = App.setting.SourceLanguageAccuracy;
            List<ComboBoxItem> items = [];

            for (int langID = 0; langID < TesseractHelper.LangList.Length - 1; langID++)
            {
                string tesseractTag = TesseractHelper.LangList[langID];
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{tesseractTag}";
                bool isInstalled = DownloadHelper.IsInstalled(langAcc, langID);

                if (App.setting.OcrEngineMode == OcrEngine.PaddleOCR) // If Paddle mode is active
                {
                    if (!PaddleHelper.LangList.Contains(tesseractTag)) // double check to ensure it exists in PaddleHelper.LangList (redundant since we loop it directly, but safe)
                        continue;
                }

                items.Add(new ComboBoxItem
                {
                    Content = text,
                    Tag = langID,
                    FontWeight = isInstalled ? FontWeights.ExtraBold : FontWeights.Normal,
                    Uid = (!isInstalled).ToString(),
                });
            }

            items = [.. items.OrderBy(o => o.Uid)];
            sourceLanguage.ItemsSource = items;

            SelectSourceLanguage();
        }

        private void LoadProvidersContent()
        {
            translationProvider.ItemsSource = Enum.GetValues<ProviderService>();
            ttsProvider.ItemsSource = Enum.GetValues<ProviderService>();
        }

        public void ButtonDownloadTesseractChanged()
        {
            downloadTesseract.IsEnabled = true;
            downloadTesseract.Visibility = Visibility.Visible;
            if (isLoadingTesseract)
            {
                downloadTesseract.IsEnabled = false;
                tesseractLoadingIcon.Visibility = Visibility.Visible;
                tesseractLoadIcon.Visibility = Visibility.Collapsed;
            }
            else if (DownloadHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
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
            SourceAccuracy accID = App.setting.SourceLanguageAccuracy;
            bool isSuccess = false;

            if (isLoadingTesseract || string.IsNullOrWhiteSpace(TesseractHelper.LangList[langID]))
                return;

            isLoadingTesseract = true;
            ButtonDownloadTesseractChanged();

            switch (App.setting.OcrEngineMode)
            {
                case OcrEngine.TesseraceOCR:
                    isSuccess = await TesseractHelper.InstallModelAsync(accID, langID);
                    if (isSuccess)
                        TesseractHelper.SaveInstalled(accID, langID);

                    break;
                case OcrEngine.PaddleOCR:
                    isSuccess = await PaddleHelper.InstallModelAsync(accID);
                    if (isSuccess)
                        PaddleHelper.SaveInstalled(accID);

                    break;
                default:
                    break;
            }

            if (isSuccess)
                SnackbarHost.Show("Source Language", $"\"{accID.GetDisplayName()} - {LanguageList.GetDisplayNameFromID(langID, true)}\" download completed successfully", SnackbarType.Success);
            else
                SnackbarHost.Show("Source Language", $"Unable to download \"{accID.GetDisplayName()} - {LanguageList.GetDisplayNameFromID(langID, true)}\"", SnackbarType.Error);

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
        private void OcrEngineMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
            {
                if (comboBox.SelectedIndex >= 0)
                    App.setting.OcrEngineMode = (OcrEngine)comboBox.SelectedIndex;

                ButtonDownloadTesseractChanged();
                LoadSourceLanguageContent();
            }
        }

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

        private void TranslationProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
            {
                if (comboBox.SelectedValue is ProviderService provider)
                {
                    App.setting.TranslationProvider = provider;
                    Translation.ChangeTranslationProvider(provider);
                }
            }
            UpdateApiSettingsVisibility();
        }

        private void UpdateApiSettingsVisibility()
        {
            if (translationProvider.SelectedValue is ProviderService provider)
            {
                ollamaApiSettingPanel.Visibility = provider == ProviderService.Ollama ? Visibility.Visible : Visibility.Collapsed;
                lmstudioApiSettingPanel.Visibility = provider == ProviderService.LMStudio ? Visibility.Visible : Visibility.Collapsed;
                openaiApiSettingPanel.Visibility = provider == ProviderService.OpenAI ? Visibility.Visible : Visibility.Collapsed;
                openrouterApiSettingPanel.Visibility = provider == ProviderService.OpenRouter ? Visibility.Visible : Visibility.Collapsed;
                deeplApiSettingPanel.Visibility = provider == ProviderService.DeepL ? Visibility.Visible : Visibility.Collapsed;
                yodaoApiSettingPanel.Visibility = (provider == ProviderService.Youdao || provider == ProviderService.Youdao) ? Visibility.Visible : Visibility.Collapsed;
                mtranserverApiSettingPanel.Visibility = provider == ProviderService.MTranServer ? Visibility.Visible : Visibility.Collapsed;
                baiduApiSettingPanel.Visibility = provider == ProviderService.Baidu ? Visibility.Visible : Visibility.Collapsed;
                libratranslateApiSettingPanel.Visibility = (provider == ProviderService.LibreTranslate || provider == ProviderService.LibreTranslate) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        private async void LMStudioLoadModelsButton_Click(object sender, RoutedEventArgs e)
        {
            SnackbarHost.Show("LMStudio", $"Getting model list from API...", SnackbarType.Info, timeout: 99999, closeButton: false);

            string baseUrl = App.setting.LMStudioConfig.ApiUrl;
            var models = await llmModelsService.FetchModelsAsync("LMStudio", baseUrl);
            lmstudioModelComboBox.ItemsSource = models;
            if (models.Count > 0)
                SnackbarHost.Show("LMStudio", $"Loaded {models.Count} models successfully", SnackbarType.Success);
            else
                SnackbarHost.Show("LMStudio", "No models found or failed to connect", SnackbarType.Error);
        }

        private async void OllamaLoadModelsButton_Click(object sender, RoutedEventArgs e)
        {
            SnackbarHost.Show("Ollama", $"Getting model list from API...", SnackbarType.Info, timeout: 99999, closeButton: false);

            string baseUrl = App.setting.OllamaConfig.ApiUrl;
            var models = await llmModelsService.FetchModelsAsync("Ollama", baseUrl);
            ollamaModelComboBox.ItemsSource = models;
            if (models.Count > 0)
                SnackbarHost.Show("Ollama", $"Loaded {models.Count} models successfully", SnackbarType.Success);
            else
                SnackbarHost.Show("Ollama", "No models found or failed to connect", SnackbarType.Error);
        }
        #endregion
    }
}
