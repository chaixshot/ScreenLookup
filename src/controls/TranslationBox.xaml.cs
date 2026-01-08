using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.controls
{
    /// <summary>
    /// Interaction logic for TranslatedBox.xaml
    /// </summary>
    public partial class TranslatedBox : UserControl
    {
        private readonly Dictionary<string, string> translatedCache = [];
        private string Original = "";
        public string Translated = "";
        private int SourceLanguage;
        private int TargetLanguage;
        private static CancellationTokenSource TranslatesCancelToken;

        public TranslatedBox()
        {
            InitializeComponent();
            ResetDefaultState();
        }

        public void Clear()
        {
            translatedCache.Clear();
            ResetDefaultState();
        }

        public async Task Translate(string text, int sourceLang, int targetLang, CancellationTokenSource token)
        {
            ResetDefaultState();

            Original = text;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;
            TranslatesCancelToken = token;

            if (!string.IsNullOrEmpty(Original))
            {
                if (!translatedCache.TryGetValue(Original, out string translatedText))
                {
                    translatedText = await Translation.GetTranslated(Original, sourceLang, targetLang);

                    if (token.IsCancellationRequested)
                        return;

                    if (string.IsNullOrEmpty(translatedText))
                        Refresh.Visibility = Visibility.Visible;
                    else
                        translatedCache.TryAdd(Original, translatedText);
                }

                Loading.Visibility = Visibility.Collapsed;

                if (!string.IsNullOrEmpty(translatedText))
                {
                    TranslatedText.Text = translatedText;
                    TranslatedText.Visibility = Visibility.Visible;
                    Refresh.Visibility = Visibility.Collapsed;

                    Translated = translatedText;
                }

                this.Tag = translatedText;
            }
        }

        public void ResetDefaultState()
        {
            double buttonWidth = App.setting.FontSizeS + 10;
            double loadingWidth = App.setting.FontSizeS + 5;

            Loading.Width = loadingWidth;
            Loading.Height = loadingWidth;

            Refresh.Width = buttonWidth;
            Refresh.Height = buttonWidth;

            TranslatedText.Text = "";
            TranslatedText.Visibility = Visibility.Collapsed;
            Loading.Visibility = Visibility.Visible;
            Refresh.Visibility = Visibility.Collapsed;

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
