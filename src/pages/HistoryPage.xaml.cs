using ScreenLookup.src.models;
using ScreenLookup.src.utils;
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

        private Grid? previousExpandedCell;

        public HistoryPage()
        {
            DataContext = this;
            InitializeComponent();

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
                previousExpandedCell = null;
            };

            SizeChanged += (s, e) =>
            {
                dataGrid.Height = App.mainWindow.ActualHeight - 212;
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

        private async void LoadHistoryLogger()
        {
            previousExpandedCell = null;
            double windowWidth = App.mainWindow.Width;
            var data = await Task.Run(() => HistoryLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage, windowWidth));

            if (data.Item2 > 0 && currentPage > data.Item2)
            {
                currentPage = data.Item2;
                data = await Task.Run(() => HistoryLogger.LoadAsync(currentPage, maxRowPerPage, SearchText, SearchSourceLanguage, windowWidth));
            }

            maxPage = (data.Item2 > 0) ? data.Item2 : 1;
            HistoryItems = data.Item1;
            PageNumber.Text = $"{currentPage}/{maxPage}";
        }

        private void ScrollTop()
        {
            if (VisualTreeHelper.GetChild(dataGrid, 0) is Decorator border)
            {
                var scrollViewer = border.Child as ScrollViewer;
                scrollViewer.ScrollToTop();
            }
        }

        private void LoadSourceLanguageItems()
        {
            if (sourceLanguage.ItemsSource != null) return;
            int langAcc = App.setting.SourceLanguageAccuracy;
            List<ComboBoxItem> items = [];
            items.Add(new ComboBoxItem()
            {
                Content = string.Empty,
                Tag = -1,
            });

            for (int langID = 0; langID < TesseractHelper.LangList.Length - 1; langID++)
            {
                string tesseractTag = TesseractHelper.LangList[langID];
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{tesseractTag}";
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

        private void SourceLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox? comboBox = (sender as ComboBox);
            ComboBoxItem? comboBoxItem = (comboBox.SelectedItem as ComboBoxItem);

            if (comboBox.IsDropDownOpen)
            {
                if (comboBoxItem != null)
                {
                    int searchSourceLanguage = Int32.Parse(comboBoxItem.Tag.ToString());
                    SearchSourceLanguage = searchSourceLanguage;
                }
                else
                    SearchSourceLanguage = -1;

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

        #region Control
        private void MaxRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox? comboBox = sender as ComboBox;

            if (comboBox.IsDropDownOpen)
            {
                string tag = (e.AddedItems[0] as ComboBoxItem).Tag as string;
                maxRowPerPage = System.Convert.ToInt32(tag);

                LoadHistoryLogger();
                ScrollTop();
            }
        }

        private async void PageDown_click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 >= 1)
            {
                currentPage--;
                LoadHistoryLogger();
                ScrollTop();
            }
        }

        private async void PageUp_click(object sender, RoutedEventArgs e)
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
            bool isYes = await DialogBox.Show("Do you want to delete all saved word?", "This operation cannot be undone!", "Yes", "No");

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
                    SnackbarHost.Show("Export", $"File saved to: \"{saveFileDialog.FileName}\"", SnackbarType.Success, width: 800);

                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("Export", $"File saved faild:{ex.Message}", SnackbarType.Error, width: 800);
                }
            }
        }

        private void HistorySearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string searchText = (sender as AutoSuggestBox)?.Text ?? string.Empty;

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
            ScrollTop();
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
                    ScrollTop();
                }
            }
        }
        #endregion


        #region Buttons

        private async void ReTranslate_Click(object sender, RoutedEventArgs e)
        {
            Button? refreshButton = sender as Button;
            StackPanel Parent = refreshButton.Parent as StackPanel;
            ProgressRing Loading = Parent.FindName("Loading") as ProgressRing;
            string Id = refreshButton.Tag.ToString();

            Loading.Visibility = Visibility.Visible;
            refreshButton.Visibility = Visibility.Collapsed;

            foreach (var item in HistoryItems)
            {
                if (item.Id == Id)
                {
                    string translatedText = await Translation.GetTranslated(isWord: false, item.Original, Int32.Parse(item.SourceLanguage), Int32.Parse(item.TargetLanguage));

                    if (string.IsNullOrEmpty(translatedText))
                        refreshButton.Visibility = Visibility.Visible;
                    else
                        HistoryLogger.Update(Int32.Parse(item.Id), translatedText);

                    break;
                }
            }

            if (this.IsLoaded)
                LoadHistoryLogger();
        }

        private async void Delete_click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            bool isYes = await DialogBox.Show($"Do you want to delete message?", "This operation cannot be undone!", "Yes", "No");

            if (isYes)
            {
                SnackbarHost.Show(
                    title: "Message",
                    message: "Revmoed",
                    type: SnackbarType.Success,
                    timeout: 2,
                    width: 130,
                    closeButton: false
                );
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

            flayOut.Show(word, string.Empty, sourceLang, targetLang);
        }

        private void Button_MessageTTS(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            TextToSpeech.StartTTS(button.Uid.ToString(), Int32.Parse(button.Tag.ToString()));
        }

        private void Button_MessageCopy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
        }
        #endregion
    }
}
