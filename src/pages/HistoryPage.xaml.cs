using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for HistoryPage.xaml
    /// </summary>
    public partial class HistoryPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int currentPage = 1;
        private int searchPage = 1;
        private int maxPage = 1;
        private int maxRowPerPage = 10;

        public string SearchText { get; set; } = string.Empty;
        public int SearchSourceLanguage = -1;

        private readonly Dictionary<string, string> translatedCache = [];

        public List<HistoryLoggerPageEntry> historyItems;

        public HistoryPage()
        {
            DataContext = this;
            InitializeComponent();

            maxRow.SelectionChanged += maxRow_SelectionChanged;

            Loaded += (s, e) =>
            {
                if (dataGrid.ItemsSource == null)
                {
                    LoadHistoryLogger();
                    LoadSourceLanguageItems();
                }
            };

            Unloaded += (s, e) =>
            {
                TextToSpeech.StopTTS();
                translatedCache.Clear();
            };

            SizeChanged += (s, e) =>
            {
                dataGrid.Height = App.mainWindow.ActualHeight - 202;
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    if (flayOut.IsOpen)
                        flayOut.IsOpen = false;
                }
            };
        }

        public void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public List<HistoryLoggerPageEntry> HistoryItems
        {
            get { return historyItems; }
            set
            {
                historyItems = value;
                OnPropertyChanged();
            }
        }

        public void LoadHistoryLogger()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(100);

                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                start:
                    var data = await HistoryLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage);

                    HistoryItems = data.Item1;
                    maxPage = (data.Item2 > 0) ? data.Item2 : 1;
                    PageNumber.Text = currentPage.ToString() + "/" + maxPage.ToString();

                    if (currentPage > maxPage)
                    {
                        currentPage = maxPage;
                        goto start;
                    }
                }));
            });
        }

        private void LoadSourceLanguageItems()
        {
            int langAcc = App.setting.SourceLanguageAccuracy;
            List<ComboBoxItem> items = [];
            items.Add(new ComboBoxItem()
            {
                Content = "",
                Tag = -1,
            });

            for (int langID = 0; langID < TesseractHelper.LangList.Length - 1; langID++)
            {
                string languageTesseract = TesseractHelper.LangList[langID];
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{languageTesseract}";
                bool isInstalled = TesseractHelper.IsInstalled(langAcc, langID);

                items.Add(new ComboBoxItem()
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
        }

        private void maxRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tag = (e.AddedItems[0] as ComboBoxItem).Tag as string;
            maxRowPerPage = System.Convert.ToInt32(tag);

            LoadHistoryLogger();
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
            LoadSourceLanguageItems();
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
            Button? button = sender as Button;
            string word = button.ToolTip.ToString();
            int sourceLang = Int32.Parse(button.Uid.ToString());
            int targetLang = Int32.Parse(button.Tag.ToString());

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.Show(word, sourceLang, targetLang);
        }

        private void Button_ParagraphTTS(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            TextToSpeech.StartTTS(button.Uid.ToString(), Int32.Parse(button.Tag.ToString()));
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
