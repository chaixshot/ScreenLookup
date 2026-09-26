using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.controls
{
    public class TranslationDataCache
    {
        public string MainTranslation { get; set; } = string.Empty;
        public List<ExtraMeaning> ExtraMeanings { get; set; } = new();
    }

    /// <summary>
    /// Interaction logic for TranslatedBox.xaml
    /// </summary>
    public partial class TranslationBox : UserControl
    {
        private readonly Dictionary<string, TranslationDataCache> translatedCache = [];

        private string Original = string.Empty;
        public string Translated = string.Empty;

        private int SourceLanguage;
        private int TargetLanguage;

        private static CancellationTokenSource TranslatesCancelToken;

        public TranslationBox()
        {
            InitializeComponent();
            ResetDefaultState();
        }

        public void Clear()
        {
            translatedCache.Clear();
            ResetDefaultState();
        }

        public void Set(string text, int sourceLang, int targetLang)
        {
            Original = text;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;

            Loading.Visibility = Visibility.Collapsed;
            Refresh.Visibility = Visibility.Visible;
            ExtraMeaningsList.Visibility = Visibility.Collapsed;
        }

        public async Task Translate(string text, int sourceLang, int targetLang, CancellationTokenSource token)
        {
            ResetDefaultState();

            Original = text;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;
            TranslatesCancelToken = token;

            if (string.IsNullOrEmpty(Original))
                return;

            // Check cache first
            if (translatedCache.TryGetValue(Original, out var cachedData))
            {
                Loading.Visibility = Visibility.Collapsed;

                if (string.IsNullOrEmpty(cachedData.MainTranslation))
                {
                    Refresh.Visibility = Visibility.Visible;
                    ExtraMeaningsList.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TranslatedText.Text = cachedData.MainTranslation;
                    TranslatedText.Visibility = Visibility.Visible;
                    Refresh.Visibility = Visibility.Collapsed;
                    Translated = cachedData.MainTranslation;

                    if (cachedData.ExtraMeanings != null && cachedData.ExtraMeanings.Count > 0)
                    {
                        ExtraMeaningsList.ItemsSource = cachedData.ExtraMeanings;
                        ExtraMeaningsList.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ExtraMeaningsList.ItemsSource = null;
                        ExtraMeaningsList.Visibility = Visibility.Collapsed;
                    }
                }

                this.Tag = cachedData.MainTranslation;
                return;
            }

            // Fetch Primary Translation FIRST
            string mainText = await Translation.GetTranslated(Original, sourceLang, targetLang);

            if (token.IsCancellationRequested)
                return;

            Loading.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(mainText))
            {
                Refresh.Visibility = Visibility.Visible;
                ExtraMeaningsList.Visibility = Visibility.Collapsed;
                return;
            }

            // Display Main Translation IMMEDIATELY
            TranslatedText.Text = mainText;
            TranslatedText.Visibility = Visibility.Visible;
            Refresh.Visibility = Visibility.Collapsed;
            Translated = mainText;
            this.Tag = mainText;

            // Cache main translation initially
            var newCache = new TranslationDataCache
            {
                MainTranslation = mainText,
                ExtraMeanings = []
            };
            translatedCache.TryAdd(Original, newCache);

            // Fetch Extra Dictionary Meanings ASYNCHRONOUSLY without delaying TranslatedText
            _ = Task.Run(async () =>
            {
                var extraMeanings = await Translation.GetExtraMeaningsAsync(Original, sourceLang, targetLang);

                if (token.IsCancellationRequested)
                    return;

                if (extraMeanings != null && extraMeanings.Count > 0)
                {
                    newCache.ExtraMeanings = extraMeanings;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        ExtraMeaningsList.ItemsSource = extraMeanings;
                        ExtraMeaningsList.Visibility = Visibility.Visible;
                    });
                }
            }, token.Token);
        }

        public void ResetDefaultState()
        {
            double buttonWidth = App.setting.FontSizeS + 10;
            double loadingWidth = App.setting.FontSizeS + 5;

            Loading.Width = loadingWidth;
            Loading.Height = loadingWidth;

            Refresh.Width = buttonWidth;
            Refresh.Height = buttonWidth;

            TranslatedText.Text = string.Empty;
            TranslatedText.Visibility = Visibility.Collapsed;
            Loading.Visibility = Visibility.Visible;
            Refresh.Visibility = Visibility.Collapsed;
            ExtraMeaningsList.ItemsSource = null;
            ExtraMeaningsList.Visibility = Visibility.Collapsed;

            Original = string.Empty;
            Translated = string.Empty;

            translatedScrollViewer.ScrollToTop();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            TranslatesCancelToken?.Cancel();
            TranslatesCancelToken = new();

            await Translate(Original, SourceLanguage, TargetLanguage, TranslatesCancelToken);
        }
    }
}
