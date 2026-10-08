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
    /// Interaction logic for SavedPage.xaml - Displays bookmarked vocabulary, priority scores, and export options.
    /// </summary>
    public partial class SavedPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        #region Fields & Properties
        private int currentPage = 1;
        private int searchPage = 1;
        private int maxPage = 1;
        private int maxRowPerPage = 30;

        private bool isDataLoaded = false;

        public string SearchText { get; set; } = string.Empty;
        public int SearchSourceLanguage { get; set; } = -1;
        public string OrderBy { get; set; } = "Score";

        private List<SavedWordEntry> _savedItems = [];
        public List<SavedWordEntry> SavedItems
        {
            get => _savedItems;
            set
            {
                _savedItems = value;
                OnPropertyChanged();
            }
        }
        #endregion


        #region Constructor & Lifecycle
        public SavedPage()
        {
            DataContext = this;
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (!isDataLoaded)
                {
                    isDataLoaded = true;
                    LoadSavedWord();
                    LoadSourceLanguageItems();
                }
            };

            Unloaded += (s, e) =>
            {
                TextToSpeech.StopTTS();
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
        private async void LoadSavedWord()
        {
            var data = await Task.Run(() => SavedWordLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage, OrderBy));

            if (data.MaxPage > 0 && currentPage > data.MaxPage)
            {
                currentPage = data.MaxPage;
                data = await Task.Run(() => SavedWordLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage, OrderBy));
            }

            maxPage = data.MaxPage > 0 ? data.MaxPage : 1;
            SavedItems = data.Entries;
            PageNumber.Text = $"{currentPage}/{maxPage}";
        }

        private void ScrollTop()
        {
            if (flayOut?.IsOpen == true)
                flayOut.IsOpen = false;

            if (dataGrid != null && VisualTreeHelper.GetChild(dataGrid, 0) is Decorator border)
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
                LoadSavedWord();
            }
        }

        private void MaxRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen && e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is ComboBoxItem item && item.Tag is string tag)
                {
                    maxRowPerPage = Convert.ToInt32(tag);
                    LoadSavedWord();
                    ScrollTop();
                }
            }
        }

        private void OrderBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen && e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is ComboBoxItem item && item.Tag is string tag)
                {
                    OrderBy = tag;
                    LoadSavedWord();
                    ScrollTop();
                }
            }
        }

        private void PageDown_click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 >= 1)
            {
                currentPage--;
                LoadSavedWord();
                ScrollTop();
            }
        }

        private void PageUp_click(object sender, RoutedEventArgs e)
        {
            if (currentPage < maxPage)
            {
                currentPage++;
                LoadSavedWord();
                ScrollTop();
            }
        }

        private async void Clear_click(object sender, RoutedEventArgs e)
        {
            bool isYes = await DialogBox.Show("Do you want to delete all saved words?", "This operation cannot be undone!", "Yes", "No");

            if (isYes)
            {
                currentPage = 1;
                SavedWordLogger.Clear();
                LoadSavedWord();
            }
        }

        private void Refresh_click(object sender, RoutedEventArgs e)
        {
            LoadSavedWord();
            ScrollTop();
            LoadSourceLanguageItems();
        }

        private async void Export_click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new()
            {
                Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"exported_saved_{DateTime.Now:yyyy-MM-dd_HH.mm.ss}.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await SavedWordLogger.ExportToCSV(saveFileDialog.FileName);
                    AppUtilities.OpenExplorer(saveFileDialog.FileName);
                    SnackbarHost.Show("Export", $"File saved to: \"{saveFileDialog.FileName}\"", SnackbarType.Success, width: 800);
                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("Export", $"File save failed: {ex.Message}", SnackbarType.Error, width: 800);
                }
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
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
            LoadSavedWord();
            ScrollTop();
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            {
                if (!string.IsNullOrEmpty(SearchText))
                {
                    SearchText = string.Empty;
                    currentPage = searchPage;
                    LoadSavedWord();
                    ScrollTop();
                }
            }
        }
        #endregion

        #region Item Action Handlers
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

        private async void Delete_click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            string word = button.Content?.ToString() ?? string.Empty;
            bool isYes = await DialogBox.Show($"Do you want to delete word \"{word}\"?", "This operation cannot be undone!", "Yes", "No");

            if (isYes)
            {
                SnackbarHost.Show(
                    title: word,
                    message: "Removed",
                    type: SnackbarType.Success,
                    timeout: 2,
                    width: 130,
                    closeButton: false
                );
                SavedWordLogger.Remove(button.Tag?.ToString() ?? string.Empty);
                LoadSavedWord();
            }
        }

        private void SubtractScore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            string word = button.Tag?.ToString() ?? string.Empty;

            button.Visibility = Visibility.Collapsed;

            SavedWordLogger.SubtractScore(word);
            SnackbarHost.Show(
                title: word,
                message: "Score -1",
                timeout: 2,
                width: 130,
                closeButton: false
            );
        }

        private void Button_TTSWord(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            TextToSpeech.StartTTS(button.Uid?.ToString() ?? string.Empty, int.Parse(button.Tag?.ToString() ?? "0"));
        }

        private void Button_WordCopy(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            Clipboard.SetText(button.Tag?.ToString() ?? string.Empty);
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
        }
        #endregion
    }
}
