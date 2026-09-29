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

        public TranslationBox()
        {
            InitializeComponent();
            ResetDefaultState();
        }

        public void Clear()
        {
            ResetDefaultState();
        }

        public void Set(string text, int sourceLang, int targetLang)
        {
            Original = text;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;

            Loading.Visibility = Visibility.Collapsed;
            Refresh.Visibility = Visibility.Visible;
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

            Original = string.Empty;
            Translated = string.Empty;

            translatedScrollViewer.ScrollToTop();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            TranslatesCancelToken?.Cancel();
            TranslatesCancelToken = new();

            await Translate(IsWord, Original, SourceLanguage, TargetLanguage, TranslatesCancelToken);
        }
    }
}
