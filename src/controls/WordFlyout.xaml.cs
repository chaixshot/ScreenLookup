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

        public int sourceLanguage = 1;
        public int targetLanguage = 1;
        public string originalWord = "";
        public string originalMessage = "";
        public double width = double.NaN;
        public bool isOpen = false;
        private static CancellationTokenSource TranslatesCancelToken;

        public WordFlyout()
        {
            DataContext = this;
            InitializeComponent();

            flayOut.Opened += OnOpen;
            flayOut.Closed += OnClose;

            this.Unloaded += (s, e) =>
            {
                ClearCache();
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

        public string OriginalMessage
        {
            get { return originalMessage; }
            set
            {
                originalMessage = value;
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

        public double FontSizeS
        {
            get { return App.setting.FontSizeS; }
            set
            {
                OnPropertyChanged();
            }
        }

        public FontFamily FontFace
        {
            get { return new(App.setting.FontFace); }
            set
            {
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static readonly DependencyProperty IsCaptureWindowProperty =
            DependencyProperty.Register("IsCapture", typeof(bool), typeof(OpenBrowserButton), new PropertyMetadata(false));
        #endregion

        public void Show(string word, string message, int sourceLang, int targetLang)
        {
            IsOpen = false;
            FontSizeS = FontSizeS;
            FontFace = FontFace;

            FollowMouse();

            word = Regex.Replace(word, @"\s*([.!?,。！？，、;{}\[\]()'‘’""])\s*", ""); // Remove punctuation
            word = char.ToUpper(word.First()) + word[1..].ToLower(); // Capitalizing first letter

            OriginalWord = word;
            OriginalMessage = message;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;


            IsOpen = true;
        }

        private void OnOpen(Flyout sender, RoutedEventArgs args)
        {
            ResetDefaultState();

            TextToSpeech.StartTTS(OriginalWord, SourceLanguage, IsCaptureWindow ? "capture" : "main");
            SavedWordButtonStateChange(OriginalWord);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    translationWord.ResetDefaultState();
                    translationMessage.ResetDefaultState();

                    TranslatesCancelToken?.Cancel();
                    TranslatesCancelToken = new();

                    // Word
                    await translationWord.Translate(OriginalWord, SourceLanguage, TargetLanguage, TranslatesCancelToken);

                    // Message
                    await translationMessage.Translate(OriginalMessage, SourceLanguage, TargetLanguage, TranslatesCancelToken);

                }));
            });
        }

        private void OnClose(Flyout sender, RoutedEventArgs args)
        {
            TextToSpeech.StopTTS();
        }

        private void FollowMouse()
        {
            Point MousePosotion = Mouse.GetPosition(this);

            mTransform.X = MousePosotion.X - 50;
            mTransform.Y = MousePosotion.Y - 40;
        }

        private void ResetDefaultState()
        {
            double buttonWidth = FontSizeS + 10;
            double loadingWidth = FontSizeS + 5;

            flayoutOriginalTSS.Width = buttonWidth;
            flayoutOriginalTSS.Height = buttonWidth;

            openBrowser.Width = buttonWidth;
            openBrowser.Height = buttonWidth;

            wordSave.Width = buttonWidth;
            wordSave.Height = buttonWidth;

            if (string.IsNullOrEmpty(OriginalMessage))
                messageSection.Visibility = Visibility.Collapsed;
            else
                messageSection.Visibility = Visibility.Visible;
        }

        public void ClearCache()
        {
            translationWord.Clear();
            translationMessage.Clear();
        }

        private async void SavedWordButtonStateChange(string word)
        {
            bool isExist = await SavedWordLogger.IsExist(word);

            wordSave.Visibility = isExist ? Visibility.Collapsed : Visibility.Visible;
            wordSaveScore.Visibility = isExist ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Button Click
        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(OriginalWord, SourceLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translationWord.Translated, TargetLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_OriginalMessageTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(OriginalMessage, SourceLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_TranslatedMessageTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translationMessage.Translated, TargetLanguage, IsCaptureWindow ? "capture" : "main");
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            string translated = translationWord.Translated;

            SnackbarHost.Show(
                title: OriginalWord,
                message: "Saved",
                type: "success",
                timeout: 2,
                width: 130,
                closeButton: false
            );
            SavedWordLogger.ToggleSaved(OriginalWord, translated, SourceLanguage, TargetLanguage);
            SavedWordButtonStateChange(OriginalWord);
        }

        private void Button_WordAddScore(object sender, RoutedEventArgs e)
        {
            var scoreButton = sender as Button;

            scoreButton.Visibility = Visibility.Collapsed;

            SavedWordLogger.AddScore(OriginalWord);
            SnackbarHost.Show(
                title: OriginalWord,
                message: "Score +1",
                timeout: 2,
                width: 130,
                closeButton: false
                );
        }

        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false, showMainWindow: !IsCaptureWindow);
        }

        #endregion
    }
}
