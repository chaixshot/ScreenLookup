using ScreenLookup.src.utils;
using ScreenLookup.src.utils.Database;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
        public string phoneticText = string.Empty;
        public int wordOccurrenceIndex = -1;
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
                UpdateOriginalMessageHighlight();
            }
        }

        public string OriginalMessage
        {
            get { return originalMessage; }
            set
            {
                originalMessage = value;
                OnPropertyChanged();
                UpdateOriginalMessageHighlight();
            }
        }

        public string PhoneticText
        {
            get { return phoneticText; }
            set
            {
                phoneticText = value;
                OnPropertyChanged();
                phoneticTextBlock?.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
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

        public void Show(string word, string message, int sourceLang, int targetLang, int occurrenceIndex = -1)
        {
            IsOpen = false;
            wordOccurrenceIndex = occurrenceIndex;
            FontSizeS = FontSizeS;
            FontFace = FontFace;

            FollowMouse();

            string stripped = AppUtilities.RegexPunctuation().Replace(word, ""); // Remove punctuation

            if (!string.IsNullOrEmpty(stripped))
                word = char.ToUpper(stripped[0]) + (stripped.Length > 1 ? stripped[1..].ToLower() : string.Empty);

            // Filter the message to only include sentences containing the processed word
            if (!string.IsNullOrEmpty(message))
            {
                bool hasCjk = word.Any(AppUtilities.IsCjk);
                string wordPattern = hasCjk ? Regex.Escape(word) : $@"(?<!\w){Regex.Escape(word)}(?!\w)";

                string[] sentences = AppUtilities.PunctuationBoundary().Split(message); // Split by punctuation, keeping the punctuation delimiters in the resulting array

                IEnumerable<string> filteredSentences = sentences.Where(s => Regex.IsMatch(s, wordPattern, RegexOptions.IgnoreCase)).Select(s => s.Trim()); // Filter sentences that contain the word (case-insensitive check against the processed word)

                message = string.Join("\n", filteredSentences); // Join them back together, adding a newline after each sentence's punctuation
                message = AppUtilities.RegexBracket().Replace(message, "");
            }

            // Clean & normalize inputs (Fixes diacritic ordering)
            OriginalWord = word.Trim().Normalize(System.Text.NormalizationForm.FormC);
            OriginalMessage = message.Trim().Normalize(System.Text.NormalizationForm.FormC);
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;

            IsOpen = true;
        }

        private void OnOpen(Flyout sender, RoutedEventArgs args)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    ResetDefaultState();

                    TextToSpeech.StartTTS(OriginalWord, SourceLanguage);
                    SavedWordButtonStateChange(OriginalWord);

                    PhoneticText = string.Empty;
                    translationWord.ResetDefaultState();
                    translationMessage.ResetDefaultState();

                    TranslatesCancelToken?.Cancel();
                    TranslatesCancelToken = new();

                    // Word
                    _ = Task.Run(async () =>
                    {
                        await Dispatcher.InvokeAsync(() => translationWord.Translate(isWord: true, OriginalWord, SourceLanguage, TargetLanguage, TranslatesCancelToken));
                    });

                    // Message
                    _ = Task.Run(async () =>
                    {
                        await Dispatcher.InvokeAsync(() => translationMessage.Translate(isWord: false, OriginalMessage, SourceLanguage, TargetLanguage, TranslatesCancelToken));
                    });

                    // Word extra details
                    _ = Task.Run(async () =>
                    {
                        var (extraMeanings, phonetic) = await Translation.GetExtraDetailsAsync(OriginalWord, SourceLanguage, TargetLanguage, TranslatesCancelToken);

                        if (!TranslatesCancelToken.IsCancellationRequested)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                PhoneticText = phonetic;

                                if (extraMeanings != null && extraMeanings.Count > 0)
                                {
                                    ExtraMeaningsList.ItemsSource = extraMeanings;
                                    ExtraMeaningsList.Visibility = Visibility.Visible;
                                }
                            });
                        }
                    });
                }));
            });
        }

        private void OnClose(Flyout sender, RoutedEventArgs args)
        {
            TranslatesCancelToken?.Cancel();
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

            ExtraMeaningsList.ItemsSource = null;
            ExtraMeaningsList.Visibility = Visibility.Collapsed;
        }

        public void ClearCache()
        {
            translationWord.Clear();
            translationMessage.Clear();
        }

        private async void SavedWordButtonStateChange(string word, bool? isExist = null)
        {
            bool exists = isExist ?? await SavedWordLogger.IsExist(word);

            wordSave.Visibility = exists ? Visibility.Collapsed : Visibility.Visible;
            wordSaveScore.Visibility = exists ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateOriginalMessageHighlight()
        {
            originalMessageBlock.Inlines.Clear();

            if (string.IsNullOrEmpty(OriginalMessage))
                return;

            if (string.IsNullOrEmpty(OriginalWord))
            {
                originalMessageBlock.Inlines.Add(new Run(OriginalMessage));
                return;
            }

            Brush accentBackground = Application.Current.TryFindResource("AccentFillColorDefaultBrush") as Brush
                                  ?? Application.Current.TryFindResource("AccentTextFillColorPrimaryBrush") as Brush
                                  ?? SystemColors.HighlightBrush;

            Brush textOnAccent = Application.Current.TryFindResource("TextOnAccentFillColorPrimaryBrush") as Brush
                              ?? Brushes.White;

            bool hasCjk = OriginalWord.Any(AppUtilities.IsCjk);
            string pattern = hasCjk
                ? $@"({Regex.Escape(OriginalWord)})"
                : $@"(?<!\w)({Regex.Escape(OriginalWord)})(?!\w)";

            string[] parts = Regex.Split(OriginalMessage, pattern, RegexOptions.IgnoreCase);

            int currentMatchIndex = 0;

            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                bool isMatch = string.Equals(part, OriginalWord, StringComparison.OrdinalIgnoreCase);
                if (isMatch && (wordOccurrenceIndex < 0 || currentMatchIndex == wordOccurrenceIndex))
                {
                    originalMessageBlock.Inlines.Add(new InlineUIContainer(new Border
                    {
                        Background = accentBackground,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(1, 1, 1, 1),
                        Child = new System.Windows.Controls.TextBlock
                        {
                            Text = part,
                            Foreground = textOnAccent,
                            FontFamily = FontFace,
                            FontSize = FontSizeS,
                            FontWeight = FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    })
                    { BaselineAlignment = BaselineAlignment.Center });
                }
                else
                {
                    originalMessageBlock.Inlines.Add(new Run(part));
                }

                if (isMatch)
                    currentMatchIndex++;
            }
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
                SavedWordButtonStateChange(OriginalWord, isExist: wordSave.Visibility != Visibility.Collapsed);
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
