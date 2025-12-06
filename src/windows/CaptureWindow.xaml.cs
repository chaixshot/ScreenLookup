using ScreenGrab;
using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TesseractOCR;
using TesseractOCR.Enums;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.windows
{
    public partial class CaptureWindow : FluentWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private CancellationTokenSource CTS = new CancellationTokenSource();

        public bool isFlyOutOpen = false;

        private class WordItem
        {
            public string Word { get; set; }
            public string Width { get; set; }
            public string Height { get; set; }
            public string Border { get; set; }
            public int FontSizeS { get; set; }
        }

        public CaptureWindow()
        {
            DataContext = this;
            InitializeComponent();
            ApplySettings();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    this.Hide();
            };
        }

        public void StartCaptureScreen()
        {
            if (!Setting.IsLanguageInstalled(Setting.SourceLanguageAccuracy, Setting.SourceLanguage))
            {
                Notification.Show($"Install {LanguageList.CultureDisplayNameFromID(Setting.SourceLanguage)} in the setting", 1000);
                this.Hide();
                return;
            }

            // Screenshot   
            Bitmap image = ScreenGrabber.CaptureDialog(false);
            if (image == null)
            {
                Notification.Show("No image has been captured", 1000);
                this.Hide();
                return;
            }

            ApplySettings();

            // Window size
            this.Width = image.Width + 50 + (Setting.FontSizeS * 5);
            this.MinWidth = this.Width;
            this.MaxWidth = this.Width;

            BitmapSource writeBmp = GetImageSourceFromBitmap(image);
            captureImage.Source = writeBmp;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Task<TesseractOCR.Page> tesseractPage = GetTesseractPageFromBitmap(image);

                    ApplyTesseractPage(tesseractPage.Result);
                }));
            });

            this.Show();
        }

        private void ApplySettings()
        {
            originalText.Text = "";
            originalTextCard.Visibility = Visibility.Collapsed;
            translatedText.FontSize = Setting.FontSizeS;
            definitionOriginal.FontSize = Setting.FontSizeS;
            definitionTranslated.FontSize = Setting.FontSizeS;
            originalTTS.Width = Setting.FontSizeS + 10;
            originalTTS.Height = Setting.FontSizeS + 10;
            translatedTSS.Width = Setting.FontSizeS + 10;
            translatedTSS.Height = Setting.FontSizeS + 10;
            flayoutOriginalTSS.Width = Setting.FontSizeS + 10;
            flayoutOriginalTSS.Height = Setting.FontSizeS + 10;
            flayoutTranslatedTSS.Width = Setting.FontSizeS + 10;
            flayoutTranslatedTSS.Height = Setting.FontSizeS + 10;
            openBrowser.Width = Setting.FontSizeS + 10;
            openBrowser.Height = Setting.FontSizeS + 10;
            wordSave.Width = Setting.FontSizeS + 10;
            wordSave.Height = Setting.FontSizeS + 10;
            captureImageCard.Visibility = Setting.ShowImage ? Visibility.Visible : Visibility.Collapsed;

            translatedTextLoading.Visibility = Visibility.Visible;
            ocrWordsLoading.Visibility = Visibility.Visible;

            ocrWords.ItemsSource = null;
            translatedText.Text = "";
        }

        private void CenterWindowOnScreen()
        {
            double screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            double screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
            double windowWidth = this.Width;
            double windowHeight = this.Height;
            this.Left = (screenWidth / 2) - (windowWidth / 2);
            this.Top = (screenHeight / 2) - (windowHeight / 2);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsFlyOutOpen
        {
            get { return isFlyOutOpen; }
            set
            {
                isFlyOutOpen = value;
                OnPropertyChanged();
            }
        }

        private void StartTTS(string Text, string Language)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            TextToSpeech.PlayTTS(Text, Language, CTS);
        }

        private async Task<TesseractOCR.Page> GetTesseractPageFromBitmap(Bitmap image)
        {
            // Image
            MemoryStream ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] fileBytes = ms.ToArray();

            // TesseractOCR
            var engine = new Engine(TesseractHelper.GetTessdataPath(), LanguageList.GetTesseractTagFromID(Setting.SourceLanguage), EngineMode.Default);
            var img = TesseractOCR.Pix.Image.LoadFromMemory(fileBytes);

            return engine.Process(img);
        }

        private async void ApplyTesseractPage(TesseractOCR.Page page)
        {
            var cardVisibility = string.IsNullOrWhiteSpace(page.Text) ? Visibility.Collapsed : Visibility.Visible;
            originalWords.Visibility = cardVisibility;
            translatedCard.Visibility = cardVisibility;

            // Original text
            originalText.Text = page.Text;

            // Original words
            List<WordItem> items = [];
            foreach (var block in page.Layout)
            {
                foreach (var paragraph in block.Paragraphs)
                {
                    foreach (var textLine in paragraph.TextLines)
                    {
                        foreach (var word in textLine.Words)
                        {
                            if (!string.IsNullOrWhiteSpace(word.Text))
                            {
                                items.Add(new WordItem() { Word = word.Text, Border = "1", FontSizeS = Setting.FontSizeS });
                            }
                        }
                        items.Add(new WordItem() { Word = "", Width = this.Width.ToString(), Height = "0" });
                    }
                    items.Add(new WordItem() { Word = "", Width = this.Width.ToString() });
                }
                items.Add(new WordItem() { Word = "", Width = this.Width.ToString() });
            }
            ocrWords.ItemsSource = items;
            ocrWordsLoading.Visibility = Visibility.Collapsed;

            // Translated text
            var translator = LanguageList.GetTranslatorService();
            var translateResult = await translator.TranslateAsync(page.Text, LanguageList.GetTesseractTagFromID(Setting.TargetLanguage));
            translatedText.Text = translateResult.Translation;
            translatedTextLoading.Visibility = Visibility.Collapsed;

            CenterWindowOnScreen();
        }

        private static BitmapSource GetImageSourceFromBitmap(Bitmap bmp)
        {
            // If you get 'dllimport unknown'-, then add 'using System.Runtime.InteropServices;'
            [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool DeleteObject([In] IntPtr hObject);

            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        // Paragraph
        private void Button_OriginalTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(originalText.Text, LanguageList.GetLanguageISO6391FromID(Setting.SourceLanguage));
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(translatedText.Text, LanguageList.GetLanguageISO6391FromID(Setting.TargetLanguage));
        }

        // Word
        private async void Button_Word(object sender, RoutedEventArgs e)
        {
            IsFlyOutOpen = false;

            Button? button = sender as Button;

            // Change flyout position follow cursor
            var MousePos_Point = Mouse.GetPosition(originalWords);
            Matrix matrix = new Matrix();
            matrix.Translate(MousePos_Point.X - 50, MousePos_Point.Y);
            mt.Matrix = matrix;
            flayOut.LayoutTransform = Transform.Identity;

            string originalWord = button.Tag.ToString();
            if (string.IsNullOrWhiteSpace(originalWord))
                return;

            IsFlyOutOpen = true;
            definitionOriginal.Text = originalWord;
            definitionTranslated.Text = "";
            definitionTranslatedLoading.Visibility = Visibility.Visible;

            SavedWordButtonStateChange(originalWord);

            StartTTS(definitionOriginal.Text, LanguageList.GetLanguageISO6391FromID(Setting.SourceLanguage));

            var translator = LanguageList.GetTranslatorService();
            var translateResult = await translator.TranslateAsync(originalWord, LanguageList.GetLanguageISO6391FromID(Setting.TargetLanguage));
            definitionTranslated.Text = translateResult.Translation;
            definitionTranslatedLoading.Visibility = Visibility.Collapsed;
        }

        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(definitionOriginal.Text, LanguageList.GetLanguageISO6391FromID(Setting.SourceLanguage));
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(definitionTranslated.Text, LanguageList.GetLanguageISO6391FromID(Setting.TargetLanguage));
        }

        // Utility
        private void App_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void Button_OpenBrowser(object sender, RoutedEventArgs e)
        {
            switch (Setting.translationProvider)
            {
                case 4:
                    Process.Start(new ProcessStartInfo($"https://translate.yandex.com/en/?source_lang={LanguageList.GetLanguageISO6391FromID(Setting.SourceLanguage)}&target_lang={LanguageList.GetLanguageISO6391FromID(Setting.TargetLanguage)}&text={definitionOriginal.Text}") { UseShellExecute = true });

                    break;
                default:
                    Process.Start(new ProcessStartInfo($"https://translate.google.com/?sl={LanguageList.GetLanguageISO6391FromID(Setting.SourceLanguage)}&tl={LanguageList.GetLanguageISO6391FromID(Setting.TargetLanguage)}&text={definitionOriginal.Text}&op=translate") { UseShellExecute = true });
                    break;
            }

        }

        private async void SavedWordButtonStateChange(string word)
        {
            var saveButton = wordSave as Button;
            var saveSymbolIcon = saveButton?.Icon as SymbolIcon;
            saveSymbolIcon?.Filled = await SavedWord.IsExist(word);
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            string word = definitionOriginal.Text;
            bool isExist = SavedWord.IsExist(word).Result;

            if (isExist)
                await SavedWord.Remove(word);
            else
                await SavedWord.Add(word, definitionTranslated.Text, Setting.SourceLanguage, Setting.TargetLanguage);
            SavedWordButtonStateChange(word);
        }
    }
}
