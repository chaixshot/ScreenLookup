using ScreenLookup.src.utils;
using System.Diagnostics;
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
    public partial class WordFlyout : UserControl
    {
        private readonly Dictionary<string, string> translatedCache = [];
        bool isCaptureWindow;

        public WordFlyout()
        {
            InitializeComponent();

            flayOut.Opened += OnOpen;
            flayOut.Closed += OnClose;

            this.Loaded += (s, e) =>
            {
                isCaptureWindow = this.Tag.ToString() == "CaptureWindow";
            };

            this.Unloaded += (s, e) =>
            {
                translatedCache.Clear();
            };
        }

        private async void OnOpen(Flyout sender, RoutedEventArgs args)
        {
            Reset();

            string word = originalWord.Text;

            // Change flyout position follow cursor
            var MousePos_Point = Mouse.GetPosition(this);
            Matrix matrix = new();
            matrix.Translate(MousePos_Point.X - 50, MousePos_Point.Y - 40);
            mt.Matrix = matrix;
            flayOut.LayoutTransform = Transform.Identity;

            TextToSpeech.StartTTS(word, Int32.Parse(originalWord.Tag.ToString()), isCaptureWindow ? "capture" : "main");
            SavedWordButtonStateChange(word);

            if (!translatedCache.TryGetValue(word, out string translateResult))
            {
                translateResult = await LanguageList.TranslatedText(word, Int32.Parse(translatedWord.Tag.ToString()));
                translatedCache.TryAdd(word, translateResult);
            }

            translatedWord.Text = translateResult;
            translatedWord.Visibility = Visibility.Visible;
            translatedWordLoading.Visibility = Visibility.Collapsed;
        }

        private void OnClose(Flyout sender, RoutedEventArgs args)
        {
            TextToSpeech.StopTTS();
        }

        private void Reset()
        {
            int buttonWidth = App.setting.FontSizeS + 10;

            originalWord.FontSize = App.setting.FontSizeS;
            originalWord.FontFamily = new FontFamily(App.setting.FontFace);

            translatedWord.FontSize = App.setting.FontSizeS;
            translatedWord.FontFamily = new FontFamily(App.setting.FontFace);

            flayoutOriginalTSS.Width = buttonWidth;
            flayoutOriginalTSS.Height = buttonWidth;
            flayoutTranslatedTSS.Width = buttonWidth;
            flayoutTranslatedTSS.Height = buttonWidth;

            openBrowser.Width = buttonWidth;
            openBrowser.Height = buttonWidth;
            wordSave.Width = buttonWidth;
            wordSave.Height = buttonWidth;

            translatedWord.Text = "";
            translatedWord.Visibility = Visibility.Collapsed;
            translatedWordLoading.Visibility = Visibility.Visible;
        }

        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(originalWord.Text, Int32.Parse(originalWord.Tag.ToString()), isCaptureWindow ? "capture" : "main");
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedWord.Text, Int32.Parse(translatedWord.Tag.ToString()), isCaptureWindow ? "capture" : "main");
        }

        private async void SavedWordButtonStateChange(string word)
        {
            var saveButton = wordSave as Button;
            var saveSymbolIcon = saveButton?.Icon as SymbolIcon;
            saveSymbolIcon?.Filled = await SavedWordLogger.IsExist(word);
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            string original = originalWord.Text;
            string translated = this.translatedWord.Text;

            if (string.IsNullOrWhiteSpace(translated) && !await SavedWordLogger.IsExist(original))
            {
                SnackbarHost.Show("Error", "Translation is not yet complete", "error", windows: isCaptureWindow ? "capture" : "main");
                return;
            }

            SavedWordLogger.ToggleSaved(original, translated, Int32.Parse(originalWord.Tag.ToString()), Int32.Parse(translatedWord.Tag.ToString()));
            SavedWordButtonStateChange(original);
        }
        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false, windows: isCaptureWindow ? "capture" : "main");
        }

        private void Button_OpenBrowser(object sender, RoutedEventArgs e)
        {
            switch (App.setting.translationProvider)
            {
                case 4:
                    Process.Start(new ProcessStartInfo($"https://translate.yandex.com/en/?source_lang={LanguageList.GetLanguageISO6391FromID(Int32.Parse(originalWord.Tag.ToString()))}&target_lang={LanguageList.GetLanguageISO6391FromID(Int32.Parse(translatedWord.Tag.ToString()))}&text={originalWord.Text}") { UseShellExecute = true });

                    break;
                default:
                    Process.Start(new ProcessStartInfo($"https://translate.google.com/?sl={LanguageList.GetLanguageISO6391FromID(Int32.Parse(originalWord.Tag.ToString()))}&tl={LanguageList.GetLanguageISO6391FromID(Int32.Parse(translatedWord.Tag.ToString()))}&text={originalWord.Text}&op=translate") { UseShellExecute = true });
                    break;
            }
        }
    }
}
