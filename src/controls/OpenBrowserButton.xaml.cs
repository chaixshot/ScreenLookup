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
            get { return (int)GetValue(SourceLanguageProperty); }
            set { SetValue(SourceLanguageProperty, value); }
        }
        public int TargetLanguage
        {
            get { return (int)GetValue(TargetLanguageProperty); }
            set { SetValue(TargetLanguageProperty, value); }
        }
        public string OriginalWord
        {
            get { return (string)GetValue(OriginalWordProperty); }
            set { SetValue(OriginalWordProperty, value); }
        }
        public int Width
        {
            get { return (int)GetValue(WidthProperty); }
            set
            {
                SetValue(WidthProperty, value);
                openBrowser.Width = value;
            }
        }
        public int Height
        {
            get { return (int)GetValue(HeightProperty); }
            set
            {
                SetValue(HeightProperty, value);
                openBrowser.Height = value;
            }
        }

        public static readonly DependencyProperty SourceLanguageProperty =
            DependencyProperty.Register("SourceLanguage", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(1));

        public static readonly DependencyProperty TargetLanguageProperty =
            DependencyProperty.Register("TargetLanguage", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(1));

        public static readonly DependencyProperty OriginalWordProperty =
            DependencyProperty.Register("OriginalWord", typeof(string), typeof(OpenBrowserButton), new PropertyMetadata(""));

        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.Register("Width", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(10));

        public static readonly DependencyProperty HeightProperty =
            DependencyProperty.Register("Height", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(10));

        public OpenBrowserButton()
        {
            InitializeComponent();
        }

        private void Button_OpenBrowser(object sender, RoutedEventArgs e)
        {
            switch (App.setting.translationProvider)
            {
                case 4:
                    Process.Start(new ProcessStartInfo($"https://translate.yandex.com/en/?source_lang={LanguageList.GetLanguageISO6391FromID(SourceLanguage)}&target_lang={LanguageList.GetLanguageISO6391FromID(TargetLanguage)}&text={OriginalWord}") { UseShellExecute = true });

                    break;
                default:
                    Process.Start(new ProcessStartInfo($"https://translate.google.com/?sl={LanguageList.GetLanguageISO6391FromID(SourceLanguage)}&tl={LanguageList.GetLanguageISO6391FromID(TargetLanguage)}&text={OriginalWord}&op=translate") { UseShellExecute = true });
                    break;
            }
        }
    }
}
