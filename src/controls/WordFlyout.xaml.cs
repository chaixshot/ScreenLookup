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
        public string originalWord = string.Empty;
        public string originalMessage = string.Empty;
        public double width = double.NaN;
        public double height = double.NaN;
        public bool isOpen = false;
        private static CancellationTokenSource TranslatesCancelToken;

        public WordFlyout()
        {
            DataContext = this;
            InitializeComponent();

            flayOut.Opened += OnOpen;
            flayOut.Closed += OnClose;

            Unloaded += (s, e) =>
            {
                ClearCache();
            };
        }

        #region
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

        public double HeightX
        {
            get { return height; }
            set
            {
                height = value;
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
        #endregion

        public void Show(string word, string message, int sourceLang, int targetLang)
        {
            IsOpen = false;
            FontSizeS = FontSizeS;
            FontFace = FontFace;

            FollowMouse();

            string stripped = Regex.Replace(word, @"\s*([.!?,。！？，、;{}\[\]()'‘’""])\s*", ""); // Remove punctuation

            if (!string.IsNullOrEmpty(stripped))
                word = char.ToUpper(stripped[0]) + (stripped.Length > 1 ? stripped[1..].ToLower() : string.Empty);

            // Filter the message to only include sentences containing the processed word
            if (!string.IsNullOrEmpty(message))
            {
                string[] sentences = Regex.Split(message, @"(?<=[.!?。！？，、;{}\[\]()])"); // Split by punctuation, keeping the punctuation delimiters in the resulting array

                IEnumerable<string> filteredSentences = sentences.Where(s => s.Contains(word, StringComparison.OrdinalIgnoreCase)).Select(s => s.Trim()); // Filter sentences that contain the word (case-insensitive check against the processed word)

                message = string.Join("\n", filteredSentences); // Join them back together, adding a newline after each sentence's punctuation
                message = Regex.Replace(message, $@"\s*([{{}}\[\]])\s*", "");
            }

            OriginalWord = word;
            OriginalMessage = message;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;

            IsOpen = true;
        }

        private void OnOpen(Flyout sender, RoutedEventArgs args)
        {
            ResetDefaultState();

            TextToSpeech.StartTTS(OriginalWord, SourceLanguage);
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
            TextToSpeech.StartTTS(OriginalWord, SourceLanguage);
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translationWord.Translated, TargetLanguage);
        }

        private async void Button_OriginalMessageTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(OriginalMessage, SourceLanguage);
        }

        private async void Button_TranslatedMessageTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translationMessage.Translated, TargetLanguage);
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            string translated = translationWord.Translated;

            if (string.IsNullOrEmpty(translated))
                SnackbarHost.Show(
                    title: "Error",
                    message: "Translation is not yet complete",
                    type: SnackbarType.Error
                );
            else
            {
                SnackbarHost.Show(
                    title: OriginalWord,
                    message: "Saved",
                    type: SnackbarType.Success,
                    timeout: 2,
                    width: 130,
                    closeButton: false
                );
                SavedWordLogger.ToggleSaved(OriginalWord, translated, SourceLanguage, TargetLanguage);
                SavedWordButtonStateChange(OriginalWord);
            }
        }

        private void Button_WordAddScore(object sender, RoutedEventArgs e)
        {
            Button? scoreButton = sender as Button;

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
            Button? button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(
                title: "Copied",
                timeout: 1,
                width: 110,
                closeButton: false
            );
        }

        #endregion
    }
}
