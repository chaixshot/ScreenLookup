using GTranslate.Translators;
using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TesseractOCR.Renderers;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for SavedPage.xaml
    /// </summary>
    public partial class SavedPage : Page
    {
        private CancellationTokenSource CTS = new CancellationTokenSource();

        private int currentPage = 1;
        private int searchPage = 1;
        private int maxPage = 1;
        private int maxRowPerPage = 50;

        public string SearchText { get; set; } = string.Empty;

        public SavedPage()
        {
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                await LoadSavedWord();
            };
            Unloaded += (s, e) =>
            {
                HistoryDataGrid.ItemsSource = null;
            };

            HistoryMaxRow.SelectionChanged += maxRow_SelectionChanged;
        }

        private void StartTTS(string Text, string Language)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            TextToSpeech.PlayTTS(Text, Language, CTS);
        }

        public async Task LoadSavedWord()
        {
            var data = await SavedWord.LoadSavedWordAsync(currentPage, maxRowPerPage, SearchText);
            List<SaveWordEntry> history = data.Item1;

            maxPage = (data.Item2 > 0) ? data.Item2 : 1;

            await Dispatcher.InvokeAsync(() =>
            {
                HistoryDataGrid.ItemsSource = history;
                PageNumber.Text = currentPage.ToString() + "/" + maxPage.ToString();
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
                    Snackbar_Show("Saved Success", $"File saved to: {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    Snackbar_Show("Save Failed", $"File saved faild:{ex.Message}");
                }
            }
        }
        private void Snackbar_Show(string title, string message, bool isError = false)
        {
            var snackbar = new Snackbar(SnackbarHost)
            {
                Title = title,
                Content = message,
                Appearance = isError ? ControlAppearance.Danger : ControlAppearance.Light,
                Timeout = TimeSpan.FromSeconds(2)
            };

            snackbar.Show();
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
            LoadSavedWord();
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
                    await LoadSavedWord();
                }
            }
        }

        private async void Delete_click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            bool isYes = await DialogBox.Show($"Do you want to saved word \"{button.Tag.ToString()}\"?", "This operation cannot be undone!", 0);

            if (isYes)
            {
                SavedWord.Remove(button.Tag.ToString());
                LoadSavedWord();
            }
        }

        private void Button_Word(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            StartTTS(button.Content.ToString(), LanguageList.GetLanguageISO6391FromID(Int32.Parse(button.Tag.ToString())));
        }
    }
}
