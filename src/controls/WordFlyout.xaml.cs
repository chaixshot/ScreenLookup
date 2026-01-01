using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.controls
{
    /// <summary>
    /// Interaction logic for WordFlyout.xaml
    /// </summary>
    public partial class WordFlyout : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly Dictionary<string, string> translatedCache = [];

        public int sourceLanguage = 1;
        public int targetLanguage = 1;
        public string originalWord = "";
        public string originalParagraph = "";
        public double width = double.NaN;
        public bool isOpen = false;

        public WordFlyout()
        {
            DataContext = this;
            InitializeComponent();

            flayOut.Opened += OnOpen;
            flayOut.Closed += OnClose;

            this.Unloaded += (s, e) =>
            {
                translatedCache.Clear();
            };
        }

        #region
        public bool IsCaptureWindow
        {
            get { return (bool)GetValue(IsCaptureWindowProperty); }
            set { SetValue(IsCaptureWindowProperty, value); }
        }

        public int SourceLanguage
        {
            get { return sourceLanguage; }
            set
            {
                sourceLanguage = value;
                OnPropertyChanged();
            }
        }

        public int TargetLanguage
        {
            get { return targetLanguage; }
            set
            {
                targetLanguage = value;
                OnPropertyChanged();
            }
        }

        public string OriginalWord
        {
            get { return originalWord; }
            set
            {
                originalWord = value;
                OnPropertyChanged();
            }
        }

        public string OriginalParagraph
        {
            get { return originalParagraph; }
            set
            {
                originalParagraph = value;
                OnPropertyChanged();
            }
        }

        public double WidthX
        {
            get { return width; }
            set
            {
                width = value;
                OnPropertyChanged();
            }
        }

        public bool IsOpen
        {
            get { return isOpen; }
            set
            {
                isOpen = value;
                OnPropertyChanged();
            }
        }

        public static double FontSizeS => App.setting.FontSizeS;
        public static FontFamily FontFace => new(App.setting.FontFace);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static readonly DependencyProperty IsCaptureWindowProperty =
            DependencyProperty.Register("IsCapture", typeof(bool), typeof(OpenBrowserButton), new PropertyMetadata(false));
        #endregion

        public void Show(string word, string paragraph, int sourceLang, int targetLang)
        {
            IsOpen = false;

            word = Regex.Replace(word, @"\s*([.!?,。！？，、])\s*", ""); // Remove punctuation
            word = char.ToUpper(word.First()) + word[1..].ToLower(); // Capitalizing first letter

            OriginalWord = word;
            OriginalParagraph = paragraph;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;

            IsOpen = true;
        }

        private void OnOpen(Flyout sender, RoutedEventArgs args)
        {
            ResetDefaultState();
            FollowMouse();

            TextToSpeech.StartTTS(OriginalWord, SourceLanguage, IsCaptureWindow ? "capture" : "main");
            SavedWordButtonStateChange(OriginalWord);

            // Word
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    if (!translatedCache.TryGetValue(OriginalWord, out string translateWord))
                    {
                        translateWord = await Translation.GetTranslated(OriginalWord, TargetLanguage);
                        translatedCache.TryAdd(OriginalWord, translateWord);
                    }

                    translatedWord.Text = translateWord;
                    translatedWord.Visibility = Visibility.Visible;
                    translatedWordLoading.Visibility = Visibility.Collapsed;
                    FollowMouse();
                }));
            });

            // Paragraph
            if (!string.IsNullOrEmpty(OriginalParagraph))
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {

                        if (!translatedCache.TryGetValue(OriginalParagraph, out string translateParagraph))
                        {
                            translateParagraph = await Translation.GetTranslated(OriginalParagraph, TargetLanguage);
                            translatedCache.TryAdd(OriginalParagraph, translateParagraph);
                        }

                        translatedParagraph.Text = translateParagraph;
                        translatedParagraph.Visibility = Visibility.Visible;
                        translatedParagraphLoading.Visibility = Visibility.Collapsed;

                        FollowMouse();
                    }));
                });
            }
        }

        private void OnClose(Flyout sender, RoutedEventArgs args)
        {
            TextToSpeech.StopTTS();
        }

        private void FollowMouse()
        {
            var MousePos_Point = Mouse.GetPosition(this);
            Matrix matrix = new();
            matrix.Translate(MousePos_Point.X - 50, MousePos_Point.Y - 40);
            mt.Matrix = matrix;
            flayOut.LayoutTransform = Transform.Identity;
        }

        private void ResetDefaultState()
        {
            double buttonWidth = App.setting.FontSizeS + 10;
            double loadingWidth = App.setting.FontSizeS + 5;

            flayoutOriginalTSS.Width = buttonWidth;
            flayoutOriginalTSS.Height = buttonWidth;

            translatedWordLoading.Width = loadingWidth;
            translatedWordLoading.Height = loadingWidth;

            openBrowser.Width = buttonWidth;
            openBrowser.Height = buttonWidth;

            wordSave.Width = buttonWidth;
            wordSave.Height = buttonWidth;

            translatedWord.Text = "";
            translatedWord.Visibility = Visibility.Collapsed;
            translatedWordLoading.Visibility = Visibility.Visible;

            translatedParagraph.Text = "";
            translatedParagraph.Visibility = Visibility.Collapsed;
            translatedParagraphLoading.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(OriginalParagraph))
                paragraphSection.Visibility = Visibility.Collapsed;
            else
                paragraphSection.Visibility = Visibility.Visible;
        }

        private async void SavedWordButtonStateChange(string word)
        {
            var saveButton = wordSave as Button;
            var saveSymbolIcon = saveButton?.Icon as SymbolIcon;
            saveSymbolIcon?.Filled = await SavedWordLogger.IsExist(word);
        }

        #region Button Click
        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(OriginalWord, SourceLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedWord.Text, TargetLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_ParagraphOriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(OriginalParagraph, SourceLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_ParagraphTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedParagraph.Text, TargetLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            string translated = this.translatedWord.Text;

            if (string.IsNullOrWhiteSpace(translated) && !await SavedWordLogger.IsExist(OriginalWord))
            {
                SnackbarHost.Show("Error", "Translation is not yet complete", "error", windows: IsCaptureWindow ? "capture" : "main");
                return;
            }

            SavedWordLogger.ToggleSaved(OriginalWord, translated, SourceLanguage, TargetLanguage);
            SavedWordButtonStateChange(OriginalWord);
        }

        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false, windows: IsCaptureWindow ? "capture" : "main");
        }
        #endregion
    }
}
