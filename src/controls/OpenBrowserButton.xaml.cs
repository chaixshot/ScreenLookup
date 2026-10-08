using ScreenLookup.src.utils;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.controls
{
    /// <summary>
    /// Interaction logic for OpenBrowserButton.xaml
    /// </summary>
    public partial class OpenBrowserButton : UserControl
    {
        public int SourceLanguage
        {
            get => (int)GetValue(SourceLanguageProperty);
            set => SetValue(SourceLanguageProperty, value);
        }

        public int TargetLanguage
        {
            get => (int)GetValue(TargetLanguageProperty);
            set => SetValue(TargetLanguageProperty, value);
        }

        public string OriginalWord
        {
            get => (string)GetValue(OriginalWordProperty);
            set => SetValue(OriginalWordProperty, value);
        }

        public static readonly DependencyProperty SourceLanguageProperty =
            DependencyProperty.Register("SourceLanguage", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(1));

        public static readonly DependencyProperty TargetLanguageProperty =
            DependencyProperty.Register("TargetLanguage", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(1));

        public static readonly DependencyProperty OriginalWordProperty =
            DependencyProperty.Register("OriginalWord", typeof(string), typeof(OpenBrowserButton), new PropertyMetadata(""));

        public OpenBrowserButton()
        {
            InitializeComponent();
        }

        private void Button_OpenBrowser(object sender, RoutedEventArgs e)
        {
            string url = App.setting.TranslationProvider switch
            {
                4 => $"https://translate.yandex.com/en/?source_lang={LanguageList.GetLanguageISO6391FromID(SourceLanguage)}&target_lang={LanguageList.GetLanguageISO6391FromID(TargetLanguage)}&text={OriginalWord}",
                _ => $"https://translate.google.com/?sl={LanguageList.GetLanguageISO6391FromID(SourceLanguage)}&tl={LanguageList.GetLanguageISO6391FromID(TargetLanguage)}&text={OriginalWord}&op=translate"
            };

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
