using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for HistoryPage.xaml
    /// </summary>
    public partial class HistoryPage : Page
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int currentPage = 1;
        private int searchPage = 1;
        private int maxPage = 1;
        private int maxRowPerPage = 10;

        public string SearchText { get; set; } = string.Empty;
        public int SearchSourceLanguage = -1;

        private readonly Dictionary<string, string> translatedCache = [];

        public HistoryPage()
        {
            InitializeComponent();
            LoadHistoryLogger();
            LoadInstalledLanguage();

            Unloaded += (s, e) =>
            {
                TextToSpeech.StopTTS();
                translatedCache.Clear();
            };

            maxRow.SelectionChanged += maxRow_SelectionChanged;
        }

        public void LoadHistoryLogger()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    var data = await HistoryLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage);

                    maxPage = (data.Item2 > 0) ? data.Item2 : 1;
                    PageNumber.Text = currentPage.ToString() + "/" + maxPage.ToString();
                    dataGrid.ItemsSource = data.Item1;
                }));
            });
        }

        private void LoadInstalledLanguage()
        {
            sourceLanguage.Items.Clear();
            sourceLanguage.Items.Add("");

            for (int langID = 0; langID < LanguageList.LanguageTesseract.Length - 1; langID++)
            {
                string languageTesseract = LanguageList.LanguageTesseract[langID];
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(42)}\t{languageTesseract}";

                ComboBoxItem item = new()
                {
                    Content = $"{text}",
                    Tag = langID,
                };

                if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, langID))
                    sourceLanguage.Items.Add(item);
            }
        }

        private void maxRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tag = (e.AddedItems[0] as ComboBoxItem).Tag as string;
            maxRowPerPage = System.Convert.ToInt32(tag);

            LoadHistoryLogger();

            if (currentPage > maxPage)
            {
                currentPage = maxPage;
                LoadHistoryLogger();
            }
        }

        private async void PageDown_click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 >= 1)
                currentPage--;
            LoadHistoryLogger();
        }

        private async void PageUp_click(object sender, RoutedEventArgs e)
        {
            if (currentPage < maxPage)
                currentPage++;
            LoadHistoryLogger();
        }

        private async void Clear_click(object sender, RoutedEventArgs e)
        {
            bool isYes = await DialogBox.Show("Do you want to delete all saved word?", "This operation cannot be undone!", 0);

            if (isYes)
            {
                currentPage = 1;
                HistoryLogger.Clear();
                LoadHistoryLogger();
            }
        }

        private void Refresh_click(object sender, RoutedEventArgs e)
        {
            LoadHistoryLogger();
        }

        private async void Export_click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|All file (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"exported_history_{DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")}.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await HistoryLogger.ExportToCSV(saveFileDialog.FileName);

                    AppUtilities.OpenExplorer(saveFileDialog.FileName);
                    SnackbarHost.Show("Export", $"File saved to: \"{saveFileDialog.FileName}\"", "success", width: 800);

                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("Export", $"File saved faild:{ex.Message}", "error", width: 800);
                }
            }
        }

        private void HistorySearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string searchText = (sender as AutoSuggestBox)?.Text ?? "";

            // Clear search by Ctrl+A and Delete and Enter
            if (string.IsNullOrEmpty(searchText))
            {
                SearchText = string.Empty;
                currentPage = searchPage;
            }
            else // Submit search
            {
                if (string.IsNullOrEmpty(SearchText))
                {
                    searchPage = currentPage;
                }
                SearchText = (sender as AutoSuggestBox)?.Text;
                currentPage = 1;
            }
            LoadHistoryLogger();
        }

        private async void HistorySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Press X to clear search box
            if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            {
                if (!string.IsNullOrEmpty(SearchText))
                {
                    SearchText = string.Empty;
                    currentPage = searchPage;
                    LoadHistoryLogger();
                }
            }
        }

        private void SourceLanguage_Changed(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (sender as ComboBox);
            var comboBoxItem = (comboBox.SelectedItem as ComboBoxItem);
            if (comboBoxItem != null)
            {
                int searchSourceLanguage = Int32.Parse(comboBoxItem.Tag.ToString());
                SearchSourceLanguage = searchSourceLanguage;
            }
            else
                SearchSourceLanguage = -1;
            LoadHistoryLogger();
        }

        private async void Delete_click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            bool isYes = await DialogBox.Show($"Do you want to delete paragraph?", "This operation cannot be undone!", 0);

            if (isYes)
            {
                HistoryLogger.Remove(button.Tag.ToString());
                LoadHistoryLogger();
            }
        }

        private async void Button_Word(object sender, RoutedEventArgs e)
        {
            flayOut.IsOpen = false;

            var button = sender as Button;
            string word = button.Content.ToString();
            int sourceLanguage = Int32.Parse(button.ToolTip.ToString());
            int targetLanguage = Int32.Parse(button.Tag.ToString());

            // Change flyout position follow cursor
            var MousePos_Point = Mouse.GetPosition(dataGrid);
            Matrix matrix = new Matrix();
            matrix.Translate(MousePos_Point.X - 50, MousePos_Point.Y + 15);
            mt.Matrix = matrix;
            flayOut.LayoutTransform = Transform.Identity;

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.IsOpen = true;

            definitionOriginal.Text = word;
            definitionOriginal.Tag = sourceLanguage;

            definitionTranslated.Text = "";
            definitionTranslated.Tag = targetLanguage;
            definitionTranslatedLoading.Visibility = Visibility.Visible;

            TextToSpeech.StartTTS(word, sourceLanguage);
            SavedWordButtonStateChange(word);

            if (!translatedCache.TryGetValue(word, out string translateResult))
            {
                translateResult = await LanguageList.TranslatedText(word, targetLanguage);
                translatedCache.TryAdd(word, translateResult);
            }

            definitionTranslated.Text = translateResult;
            definitionTranslatedLoading.Visibility = Visibility.Collapsed;
        }

        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(definitionOriginal.Text, App.setting.SourceLanguage);
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(definitionTranslated.Text, App.setting.TargetLanguage);
        }

        private void Button_OpenBrowser(object sender, RoutedEventArgs e)
        {
            switch (App.setting.TranslationProvider)
            {
                case 4:
                    Process.Start(new ProcessStartInfo($"https://translate.yandex.com/en/?source_lang={LanguageList.GetLanguageISO6391FromID(App.setting.SourceLanguage)}&target_lang={LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage)}&text={definitionOriginal.Text}") { UseShellExecute = true });

                    break;
                default:
                    Process.Start(new ProcessStartInfo($"https://translate.google.com/?sl={LanguageList.GetLanguageISO6391FromID(App.setting.SourceLanguage)}&tl={LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage)}&text={definitionOriginal.Text}&op=translate") { UseShellExecute = true });
                    break;
            }
        }

        private async void SavedWordButtonStateChange(string word)
        {
            var saveButton = wordSave as Button;
            var saveSymbolIcon = saveButton?.Icon as SymbolIcon;
            saveSymbolIcon?.Filled = await SavedWordLogger.IsExist(word);
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(definitionTranslated.Text))
            {
                SnackbarHost.Show("Error", "Translation is not yet complete", "error");
                return;
            }

            string word = definitionOriginal.Text;

            SavedWordLogger.ToggleSaved(word, definitionTranslated.Text, Int32.Parse(definitionOriginal.Tag.ToString()), Int32.Parse(definitionTranslated.Tag.ToString()));
            SavedWordButtonStateChange(word);
        }

        private void Button_ParagraphTTS(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            TextToSpeech.StartTTS(button.ToolTip.ToString(), Int32.Parse(button.Tag.ToString()));
        }

        private void Button_ParagraphCopy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
        }

        private void original_MouseEnter(object sender, MouseEventArgs e)
        {
            var parent = sender as Grid;
            var originalWords = parent.FindName("originalWords") as ItemsControl;
            var original = parent.FindName("original") as Wpf.Ui.Controls.TextBlock;

            originalWords.Visibility = Visibility.Visible;
            original.Visibility = Visibility.Collapsed;
        }
    }
}
