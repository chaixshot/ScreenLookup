using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.controls
{
    /// <summary>
    /// Interaction logic for TranslatedBox.xaml
    /// </summary>
    public partial class TranslationBox : UserControl
    {
        private string Original = string.Empty;
        public string Translated = string.Empty;

        private int SourceLanguage;
        private int TargetLanguage;
        private bool IsWord;

        private static CancellationTokenSource TranslatesCancelToken;

        public double ButtonWidth
        {
            get => (double)GetValue(ButtonWidthProperty);
            set => SetValue(ButtonWidthProperty, value);
        }

        public static readonly DependencyProperty ButtonWidthProperty =
            DependencyProperty.Register(
                nameof(ButtonWidth),
                typeof(double),
                typeof(TranslationBox),
                new PropertyMetadata(App.setting.ButtonWidth)
            );

        public TranslationBox()
        {
            InitializeComponent();
            ResetDefaultState();
        }

        public void Clear()
        {
            ResetDefaultState();
        }

        public void SetParagraph(string paragraph, int sourceLang, int targetLang)
        {
            Original = paragraph;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;

            Loading.Visibility = Visibility.Collapsed;
            Refresh.Visibility = Visibility.Visible;
        }

        public void SetOriginal(string word, int sourceLang, int targetLang)
        {
            Original = word;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;
            IsWord = true;

            Loading.Visibility = Visibility.Collapsed;
            Refresh.Visibility = Visibility.Collapsed;
            TranslatedText.Text = word;
            TranslatedText.Visibility = Visibility.Visible;
            Translated = word;
            this.Tag = word;
        }

        public async Task Translate(bool isWord, string text, int sourceLang, int targetLang, CancellationTokenSource token)
        {
            ResetDefaultState();

            Original = text;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;
            IsWord = isWord;
            TranslatesCancelToken = token;

            if (string.IsNullOrEmpty(Original))
                return;

            string mainText = await Translation.GetTranslated(IsWord, Original, sourceLang, targetLang);

            if (token.IsCancellationRequested)
                return;

            Loading.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(mainText))
            {
                Refresh.Visibility = Visibility.Visible;
                return;
            }

            // Display Main Translation
            TranslatedText.Text = mainText;
            TranslatedText.Visibility = Visibility.Visible;
            Refresh.Visibility = Visibility.Collapsed;
            Translated = mainText;
            this.Tag = mainText;
        }

        public async Task UpdateExtraMeanings(List<ExtraMeaningEntity> extraMeanings)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (extraMeanings != null && extraMeanings.Count > 0)
                {
                    ExtraMeaningsList.ItemsSource = extraMeanings;
                    ExtraMeaningsList.Visibility = Visibility.Visible;
                }
                else
                {
                    ExtraMeaningsList.ItemsSource = null;
                    ExtraMeaningsList.Visibility = Visibility.Collapsed;
                }
            });
        }

        public void UpdatePhonetic(string phonetic)
        {
            if (!string.IsNullOrEmpty(phonetic))
            {
                PhoneticTextElement.Text = phonetic;
                PhoneticTextElement.Visibility = Visibility.Visible;
            }
            else
            {
                PhoneticTextElement.Text = string.Empty;
                PhoneticTextElement.Visibility = Visibility.Collapsed;
            }
        }

        public void ResetDefaultState()
        {
            TranslatedText.Text = string.Empty;
            TranslatedText.Visibility = Visibility.Collapsed;
            Loading.Visibility = Visibility.Visible;
            Refresh.Visibility = Visibility.Collapsed;

            PhoneticTextElement.Text = string.Empty;
            PhoneticTextElement.Visibility = Visibility.Collapsed;

            ExtraMeaningsList.ItemsSource = null;
            ExtraMeaningsList.Visibility = Visibility.Collapsed;

            Original = string.Empty;
            Translated = string.Empty;
            UpdatePhonetic(string.Empty);

            translatedScrollViewer.ScrollToTop();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            TranslatesCancelToken?.Cancel();
            TranslatesCancelToken = new();

            await Translate(IsWord, Original, SourceLanguage, TargetLanguage, TranslatesCancelToken);
        }

        private void SpeakButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Translated))
            {
                int lang = TargetLanguage != -1 ? TargetLanguage : SourceLanguage;
                TextToSpeech.StartTTS(Translated, lang);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Translated))
            {
                Clipboard.SetText(Translated);
                SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
            }
        }
    }
}
