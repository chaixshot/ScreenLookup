using HunspellSharp;
using ScreenGrab;
using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
using FontFamily = System.Windows.Media.FontFamily;

namespace ScreenLookup.src.windows
{
    public partial class CaptureWindow : FluentWindow
    {
        public CaptureWindow()
        {
            InitializeComponent();
            ApplySettings();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    HideWindow();
            };
        }

        private void HideWindow()
        {
            this.Hide();
            TextToSpeech.StopTTS();
        }

        public void StartCaptureScreen()
        {
            if (!Setting.IsTesseractInstalled(Setting.SourceLanguageAccuracy, Setting.SourceLanguage))
            {
                Notification.Show($"You have to install {LanguageList.GetDisplayNameFromID(Setting.SourceLanguage, true)} in the setting");
                HideWindow();
                return;
            }

            // Screenshot   
            Bitmap image = ScreenGrabber.CaptureDialog(false);
            if (image == null)
            {
                Notification.Show("No image has been captured");
                HideWindow();
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

            CenterWindowOnScreen();
            this.Show();
            this.Activate();
        }

        private void ApplySettings()
        {
            int buttonWidth = Setting.FontSizeS + 10;
            originalText.Text = "";
            originalTextCard.Visibility = Visibility.Collapsed;

            translatedText.FontSize = Setting.FontSizeS;
            translatedText.FontFamily = new FontFamily(Setting.FontFace);

            definitionOriginal.FontSize = Setting.FontSizeS;
            definitionOriginal.FontFamily = new FontFamily(Setting.FontFace);

            definitionTranslated.FontSize = Setting.FontSizeS;
            definitionTranslated.FontFamily = new FontFamily(Setting.FontFace);

            originalTTS.Width = buttonWidth;
            originalTTS.Height = buttonWidth;

            translatedTSS.Width = buttonWidth;
            translatedTSS.Height = buttonWidth;

            flayoutOriginalTSS.Width = buttonWidth;
            flayoutOriginalTSS.Height = buttonWidth;
            flayoutTranslatedTSS.Width = buttonWidth;
            flayoutTranslatedTSS.Height = buttonWidth;

            openBrowser.Width = buttonWidth;
            openBrowser.Height = buttonWidth;
            wordSave.Width = buttonWidth;
            wordSave.Height = buttonWidth;

            captureImageCard.Visibility = Setting.ShowImage ? Visibility.Visible : Visibility.Collapsed;

            translatedTextLoading.Visibility = Visibility.Visible;
            ocrWordsLoading.Visibility = Visibility.Visible;

            ocrWords.ItemsSource = null;
            translatedText.Text = "";

            this.Topmost = Setting.Topmost;
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
            List<CaptureWordsEntrySimplify> items = [];
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
                                string text = word.Text;

                                if (Setting.HunSpell)
                                {
                                    var hunspell = new Hunspell($"{HunspellHelper.FilePath}\\en_US.aff", $"{HunspellHelper.FilePath}\\en_US.dic");
                                    if (!hunspell.Spell(word.Text))
                                    {
                                        List<string> suggestions = hunspell.Suggest(word.Text);
                                        if (suggestions.Count != 0)
                                            text = suggestions[0];
                                    }
                                }
                                items.Add(new CaptureWordsEntrySimplify() { Word = text, Stop = 0 });
                            }
                        }
                        items.Add(new CaptureWordsEntrySimplify() { Word = "", Stop = 1 });
                    }
                    items.Add(new CaptureWordsEntrySimplify() { Word = "", Stop = 2 });
                }
                items.Add(new CaptureWordsEntrySimplify() { Word = "", Stop = 3 });
            }
            ocrWords.ItemsSource = Convertor.ConvertCaptureWordsEntry(items);
            ocrWordsLoading.Visibility = Visibility.Collapsed;

            // Translated text
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                var translator = LanguageList.GetTranslatorService(Setting.TranslationProvider);
                var translateResult = await translator.TranslateAsync(page.Text, LanguageList.GetTesseractTagFromID(Setting.TargetLanguage));
                translatedText.Text = translateResult.Translation;
                translatedTextLoading.Visibility = Visibility.Collapsed;
            }

            AddToHistory(originalText.Text, items, translatedText.Text);
            CenterWindowOnScreen();
        }

        private async void AddToHistory(string original, List<CaptureWordsEntrySimplify> originalWords, string translated)
        {
            await HistoryLogger.Add(original, originalWords, translated, Setting.SourceLanguage, Setting.TargetLanguage);
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
            TextToSpeech.StartTTS(originalText.Text, Setting.SourceLanguage);
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedText.Text, Setting.TargetLanguage);
        }

        // Word
        private async void Button_Word(object sender, RoutedEventArgs e)
        {
            flayOut.IsOpen = false;

            Button? button = sender as Button;
            string word = button.Content.ToString();

            // Change flyout position follow cursor
            var MousePos_Point = Mouse.GetPosition(originalWords);
            Matrix matrix = new Matrix();
            matrix.Translate(MousePos_Point.X - 50, MousePos_Point.Y);
            mt.Matrix = matrix;
            flayOut.LayoutTransform = Transform.Identity;

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.IsOpen = true;
            definitionOriginal.Text = word;
            definitionTranslated.Text = "";
            definitionTranslatedLoading.Visibility = Visibility.Visible;

            TextToSpeech.StartTTS(definitionOriginal.Text, Setting.SourceLanguage);
            SavedWordButtonStateChange(word);

            string translateResult = await LanguageList.TranslatedText(word, Setting.TargetLanguage);
            definitionTranslated.Text = translateResult;
            definitionTranslatedLoading.Visibility = Visibility.Collapsed;
        }

        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(definitionOriginal.Text, Setting.SourceLanguage);
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(definitionTranslated.Text, Setting.TargetLanguage);
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
            if (string.IsNullOrWhiteSpace(definitionTranslated.Text))
            {
                SnackbarHost.Show("Error", "Translation is not yet complete", "error", windows: "capture");
                return;
            }

            string word = definitionOriginal.Text;

            SavedWord.ToggleSaved(word, definitionTranslated.Text, Setting.SourceLanguage, Setting.TargetLanguage);
            SavedWordButtonStateChange(word);
        }

        // Utility
        private void App_Deactivated(object sender, EventArgs e)
        {
            if (Setting.CloseLostFocus)
                HideWindow();
        }
    }
}
