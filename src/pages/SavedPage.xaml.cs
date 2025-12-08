using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for SavedPage.xaml
    /// </summary>
    public partial class SavedPage : Page
    {
        private CancellationTokenSource CTS = new();

        private int currentPage = 1;
        private int searchPage = 1;
        private int maxPage = 1;
        private int maxRowPerPage = 30;

        public string SearchText { get; set; } = string.Empty;

        public SavedPage()
        {
            InitializeComponent();
            LoadSavedWord();

            Unloaded += (s, e) =>
            {
                CTS.Cancel();
            };

            maxRow.SelectionChanged += maxRow_SelectionChanged;
        }

        private void StartTTS(string Text, int langID)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            TextToSpeech.PlayTTS(Text, langID, CTS);
        }

        public void LoadSavedWord()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    var data = await SavedWord.LoadAsync(currentPage, maxRowPerPage, SearchText);
                    List<SavedWordEntry> saved = data.Item1;

                    maxPage = (data.Item2 > 0) ? data.Item2 : 1;

                    SavedDataGrid.ItemsSource = saved;
                    PageNumber.Text = currentPage.ToString() + "/" + maxPage.ToString();
                }));
            });
        }

        private void maxRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tag = (e.AddedItems[0] as ComboBoxItem).Tag as string;
            maxRowPerPage = Convert.ToInt32(tag);

            LoadSavedWord();

            if (currentPage > maxPage)
            {
                currentPage = maxPage;
                LoadSavedWord();
            }
        }

        private async void PageDown_click(object sender, RoutedEventArgs e)
        {
            if (currentPage - 1 >= 1)
                currentPage--;
            LoadSavedWord();
        }

        private async void PageUp_click(object sender, RoutedEventArgs e)
        {
            if (currentPage < maxPage)
                currentPage++;
            LoadSavedWord();
        }

        private async void Clear_click(object sender, RoutedEventArgs e)
        {
            bool isYes = await DialogBox.Show("Do you want to delete all saved word?", "This operation cannot be undone!", 0);

            if (isYes)
            {
                currentPage = 1;
                SavedWord.Clear();
                LoadSavedWord();
            }
        }

        private void Refresh_click(object sender, RoutedEventArgs e)
        {
            LoadSavedWord();
        }

        private async void Export_click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|All file (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"exported_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await SavedWord.ExportToCSV(saveFileDialog.FileName);
                    SnackbarHost.Show("Export", $"File saved to: \"{saveFileDialog.FileName}\"", "success");
                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("Export", $"File saved faild:{ex.Message}", "error");
                }
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
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
            LoadSavedWord();
        }

        private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Press X to clear search box
            if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            {
                if (!string.IsNullOrEmpty(SearchText))
                {
                    SearchText = string.Empty;
                    currentPage = searchPage;
                    LoadSavedWord();
                }
            }
        }

        private async void Delete_click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            bool isYes = await DialogBox.Show($"Do you want to delete word \"{button.Tag.ToString()}\"?", "This operation cannot be undone!", 0);

            if (isYes)
            {
                SavedWord.Remove(button.Tag.ToString());
                LoadSavedWord();
            }
        }

        private void Button_TTSWord(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            StartTTS(button.ToolTip.ToString(), Int32.Parse(button.Tag.ToString()));
        }

        private void Button_WordCopy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.ToolTip.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 150);
        }
    }
}
