using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using ScreenLookup.src.utils.Database;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for HistoryPage.xaml - Displays OCR lookup history, full sentence translations, and word cards.
    /// </summary>
    public partial class HistoryPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        #region Fields & Properties
        private int currentPage = 1;
        private int searchPage = 1;
        private int maxPage = 1;
        private int maxRowPerPage = 10;

        private bool isDataLoaded = false;

        public string SearchText { get; set; } = string.Empty;
        public int SearchSourceLanguage { get; set; } = -1;

        private readonly Dictionary<string, string> translatedCache = [];
        private List<HistoryLoggerPageEntry> _historyItems = [];
        private Grid? previousExpandedCell;

        public List<HistoryLoggerPageEntry> HistoryItems
        {
            get => _historyItems;
            set
            {
                _historyItems = value;
                OnPropertyChanged();
            }
        }
        #endregion


        #region Constructor & Lifecycle
        public HistoryPage()
        {
            DataContext = this;
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (!isDataLoaded)
                {
                    isDataLoaded = true;
                    LoadHistoryLogger();
                    LoadSourceLanguageItems();
                }
            };

            Unloaded += (s, e) =>
            {
                TextToSpeech.StopTTS();
                translatedCache.Clear();
                previousExpandedCell = null;
            };

            SizeChanged += (s, e) =>
            {
                dataGrid.Height = Math.Max(100, App.mainWindow.ActualHeight - 212);
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && flayOut.IsOpen)
                {
                    flayOut.IsOpen = false;
                }
            };
        }

        public void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        #endregion

        #region Data Loading & UI Helpers
        private async void LoadHistoryLogger()
        {
            previousExpandedCell = null;
            double windowWidth = App.mainWindow.Width;
            var data = await Task.Run(() => HistoryLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage, windowWidth));

            if (data.MaxPage > 0 && currentPage > data.MaxPage)
            {
                currentPage = data.MaxPage;
                data = await Task.Run(() => HistoryLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage, windowWidth));
            }

            maxPage = data.MaxPage > 0 ? data.MaxPage : 1;
            HistoryItems = data.Entries;
            PageNumber.Text = $"{currentPage}/{maxPage}";
        }

        private void ScrollTop()
        {
            if (VisualTreeHelper.GetChild(dataGrid, 0) is Decorator border)
            {
                if (border.Child is ScrollViewer scrollViewer)
                    scrollViewer.ScrollToTop();
            }
        }

        private void LoadSourceLanguageItems()
        {
            if (sourceLanguage.ItemsSource != null) return;
            int langAcc = App.setting.SourceLanguageAccuracy;
            List<ComboBoxItem> items =
            [
                new ComboBoxItem
                {
                    Content = string.Empty,
                    Tag = -1,
                }
            ];

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
        }
        #endregion

        #region Control Event Handlers
        private void SourceLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem comboBoxItem && comboBox.IsDropDownOpen)
            {
                SearchSourceLanguage = int.Parse(comboBoxItem.Tag.ToString() ?? "-1");
                LoadHistoryLogger();
            }
        }

        private async void Original_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not Grid parent) return;

            if (parent.Children.Count >= 3)
            {
                UIElement originalWords = parent.Children[1];
                if (originalWords.Visibility != Visibility.Visible)
                {
                    await Task.Delay(150);
                    if (!parent.IsMouseOver) return;

                    if (previousExpandedCell != null && previousExpandedCell != parent && previousExpandedCell.Children.Count >= 3)
                    {
                        previousExpandedCell.Children[1].Visibility = Visibility.Collapsed;
                        previousExpandedCell.Children[2].Visibility = Visibility.Visible;
                    }
                    previousExpandedCell = parent;

                    originalWords.Visibility = Visibility.Visible;
                    parent.Children[2].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void MaxRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen && e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is ComboBoxItem item && item.Tag is string tag)
                {
                    maxRowPerPage = Convert.ToInt32(tag);
                    LoadHistoryLogger();
                    ScrollTop();
                }
            }
        }

        private void PageDown_click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 >= 1)
            {
                currentPage--;
                LoadHistoryLogger();
                ScrollTop();
            }
        }

        private void PageUp_click(object sender, RoutedEventArgs e)
        {
            if (currentPage < maxPage)
            {
                currentPage++;
                LoadHistoryLogger();
                ScrollTop();
            }
        }

        private async void Clear_click(object sender, RoutedEventArgs e)
        {
            bool isYes = await DialogBox.Show("Do you want to delete all history entries?", "This operation cannot be undone!", "Yes", "No");

            if (isYes)
            {
                currentPage = 1;
                HistoryLogger.Clear();
                LoadHistoryLogger();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadHistoryLogger();
            ScrollTop();
            LoadSourceLanguageItems();
        }

        private async void Export_click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new()
            {
                Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"exported_history_{DateTime.Now:yyyy-MM-dd_HH.mm.ss}.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await HistoryLogger.ExportToCSV(saveFileDialog.FileName);
                    AppUtilities.OpenExplorer(saveFileDialog.FileName);
                    SnackbarHost.Show("Export", $"File saved to: \"{saveFileDialog.FileName}\"", SnackbarType.Success, width: 800);
                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("Export", $"File save failed: {ex.Message}", SnackbarType.Error, width: 800);
                }
            }
        }

        private void HistorySearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string searchText = sender.Text ?? string.Empty;

            if (string.IsNullOrEmpty(searchText))
            {
                SearchText = string.Empty;
                currentPage = searchPage;
            }
            else
            {
                if (string.IsNullOrEmpty(SearchText))
                {
                    searchPage = currentPage;
                }
                SearchText = searchText;
                currentPage = 1;
            }
            LoadHistoryLogger();
            ScrollTop();
        }

        private void HistorySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            {
                if (!string.IsNullOrEmpty(SearchText))
                {
                    SearchText = string.Empty;
                    currentPage = searchPage;
                    LoadHistoryLogger();
                    ScrollTop();
                }
            }
        }
        #endregion

        #region Item Action Handlers
        private async void ReTranslate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button refreshButton) return;
            if (refreshButton.Parent is not StackPanel parent) return;
            if (parent.FindName("Loading") is not ProgressRing loading) return;

            string id = refreshButton.Tag?.ToString() ?? string.Empty;

            loading.Visibility = Visibility.Visible;
            refreshButton.Visibility = Visibility.Collapsed;

            foreach (var item in HistoryItems)
            {
                if (item.Id == id)
                {
                    string translatedText = await Translation.GetTranslated(isWord: false, item.Original, int.Parse(item.SourceLanguage), int.Parse(item.TargetLanguage));

                    if (string.IsNullOrEmpty(translatedText))
                        refreshButton.Visibility = Visibility.Visible;
                    else
                        HistoryLogger.Update(int.Parse(item.Id), translatedText);

                    break;
                }
            }

            if (this.IsLoaded)
                LoadHistoryLogger();
        }

        private async void Delete_click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            bool isYes = await DialogBox.Show("Do you want to delete this message?", "This operation cannot be undone!", "Yes", "No");

            if (isYes)
            {
                SnackbarHost.Show(
                    title: "Message",
                    message: "Removed",
                    type: SnackbarType.Success,
                    timeout: 2,
                    width: 130,
                    closeButton: false
                );
                HistoryLogger.Remove(button.Tag?.ToString() ?? string.Empty);
                LoadHistoryLogger();
            }
        }

        private void Button_Word(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            string word = button.ToolTip?.ToString() ?? string.Empty;
            int sourceLang = int.Parse(button.Uid?.ToString() ?? "0");
            int targetLang = int.Parse(button.Tag?.ToString() ?? "0");

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.Show(word, string.Empty, sourceLang, targetLang);
        }

        private void Button_MessageTTS(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            TextToSpeech.StartTTS(button.Uid?.ToString() ?? string.Empty, int.Parse(button.Tag?.ToString() ?? "0"));
        }

        private void Button_MessageCopy(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            Clipboard.SetText(button.Tag?.ToString() ?? string.Empty);
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
        }
        #endregion
    }
}
