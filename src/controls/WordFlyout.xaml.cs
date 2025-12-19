using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        private readonly Dictionary<string, string> translatedCache = [];
        bool isCaptureWindow;

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
            set
            {
                SetValue(OriginalWordProperty, value);
                OnPropertyChanged();
            }
        }

        public WordFlyout()
        {
            DataContext = this;
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

        public static readonly DependencyProperty SourceLanguageProperty =
            DependencyProperty.Register("SourceLanguage_WordFlyout", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(1));

        public static readonly DependencyProperty TargetLanguageProperty =
            DependencyProperty.Register("TargetLanguage_WordFlyout", typeof(int), typeof(OpenBrowserButton), new PropertyMetadata(1));

        public static readonly DependencyProperty OriginalWordProperty =
            DependencyProperty.Register("OriginalWord_WordFlyout", typeof(string), typeof(OpenBrowserButton), new PropertyMetadata(""));

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void OnOpen(Flyout sender, RoutedEventArgs args)
        {
            Reset();

            // Change flyout position follow cursor
            var MousePos_Point = Mouse.GetPosition(this);
            Matrix matrix = new();
            matrix.Translate(MousePos_Point.X - 50, MousePos_Point.Y - 40);
            mt.Matrix = matrix;
            flayOut.LayoutTransform = Transform.Identity;

            TextToSpeech.StartTTS(OriginalWord, SourceLanguage, isCaptureWindow ? "capture" : "main");
            SavedWordButtonStateChange(OriginalWord);

            if (!translatedCache.TryGetValue(OriginalWord, out string translateResult))
            {
                translateResult = await LanguageList.TranslatedText(OriginalWord, TargetLanguage);
                translatedCache.TryAdd(OriginalWord, translateResult);
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
            TextToSpeech.StartTTS(OriginalWord, SourceLanguage, isCaptureWindow ? "capture" : "main");
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedWord.Text, TargetLanguage, isCaptureWindow ? "capture" : "main");
        }

        private async void SavedWordButtonStateChange(string word)
        {
            var saveButton = wordSave as Button;
            var saveSymbolIcon = saveButton?.Icon as SymbolIcon;
            saveSymbolIcon?.Filled = await SavedWordLogger.IsExist(word);
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            string translated = this.translatedWord.Text;

            if (string.IsNullOrWhiteSpace(translated) && !await SavedWordLogger.IsExist(OriginalWord))
            {
                SnackbarHost.Show("Error", "Translation is not yet complete", "error", windows: isCaptureWindow ? "capture" : "main");
                return;
            }

            SavedWordLogger.ToggleSaved(OriginalWord, translated, SourceLanguage, TargetLanguage);
            SavedWordButtonStateChange(OriginalWord);
        }
        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false, windows: isCaptureWindow ? "capture" : "main");
        }
    }
}
