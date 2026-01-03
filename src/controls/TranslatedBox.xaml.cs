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
        private string Text = "";
        private int TargetLanguage;

        public TranslatedBox()
        {
            InitializeComponent();
            ResetDefaultState();
        }

        public void ClearCache()
        {
            translatedCache.Clear();
        }

        public async Task TranslateText(string text, int targetLang)
        {
            Translated.Text = "";
            Loading.Visibility = Visibility.Visible;
            Refresh.Visibility = Visibility.Visible;

            Text = text;
            TargetLanguage = targetLang;

            if (!string.IsNullOrEmpty(Text))
            {
                if (!translatedCache.TryGetValue(Text, out string translateWord))
                {
                    translateWord = await Translation.GetTranslated(Text, targetLang);
                    if (!string.IsNullOrEmpty(translateWord))
                        translatedCache.TryAdd(Text, translateWord);
                }

                Loading.Visibility = Visibility.Collapsed;

                if (!string.IsNullOrEmpty(translateWord))
                {
                    Translated.Text = translateWord;
                    Refresh.Visibility = Visibility.Collapsed;
                }

                this.Tag = translateWord;
            }
        }

        private void ResetDefaultState()
        {
            double buttonWidth = App.setting.FontSizeS + 10;
            double loadingWidth = App.setting.FontSizeS + 5;

            Loading.Width = loadingWidth;
            Loading.Height = loadingWidth;

            Refresh.Width = buttonWidth;
            Refresh.Height = buttonWidth;

            translatedScrollViewer.ScrollToTop();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            TranslateText(Text, TargetLanguage);
        }
    }
}
