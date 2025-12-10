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
        private readonly Dictionary<string, string> translatedCache = [];
        public CaptureWindow()
        {
            InitializeComponent();
            ResetDefaultState();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    HideWindow();
            };
        }

        private void HideWindow()
        {
            this.Hide();
            translatedCache.Clear();
            TextToSpeech.StopTTS();
        }

        private void ShowWindow()
        {
            this.Show();
            this.Activate();
        }

        public async void StartCaptureScreen()
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

            ResetDefaultState();
            ChangeCaptureImage(image);
            CenterWindowOnScreen(image.Width, image.Height);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    TesseractOCR.Page tesseract = await GetTesseractPageFromBitmap(image);

                    if (!string.IsNullOrWhiteSpace(tesseract.Text))
                    {
                        originalCard.Visibility = Visibility.Visible;
                        translatedCard.Visibility = Visibility.Visible;

                        // Original full paragraph
                        ocrText.Text = tesseract.Text;

                        // Original words card
                        List<CaptureWordsEntrySimplify> captureWords = await TesseractCaptureWordsySimplify(tesseract);
                        originalWords.ItemsSource = Convertor.ConvertCaptureWordsEntry(captureWords, width: this.Width);
                        originalWordsLoading.Visibility = Visibility.Collapsed;

                        // Translate card
                        string translateResult = await LanguageList.TranslatedText(tesseract.Text, Setting.TargetLanguage);
                        translatedText.Text = translateResult;
                        translatedTextLoading.Visibility = Visibility.Collapsed;

                        await AddToHistory(ocrText.Text, captureWords, translateResult);

                        CenterWindowOnScreen(image.Width, image.Height);
                    }
                }));
            });

            ShowWindow();
        }

        private void ResetDefaultState()
        {
            int buttonWidth = Setting.FontSizeS + 10;
            ocrText.Text = "";
            ocrCard.Visibility = Visibility.Collapsed;

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
            originalWordsLoading.Visibility = Visibility.Visible;

            originalCard.Visibility = Visibility.Collapsed;
            translatedCard.Visibility = Visibility.Collapsed;

            originalWords.ItemsSource = null;
            translatedText.Text = "";

            this.Topmost = Setting.Topmost;
        }

        private void CenterWindowOnScreen(double imgWidth, double imgHeight)
        {
            double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
            double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

            captureImage.Width = Math.Min(imgWidth, screenWidth);
            captureImage.Height = Math.Min(imgHeight, screenHeight / 2);

            this.MaxWidth = screenWidth - 100;
            this.MaxHeight = screenHeight - 100;
            this.Width = captureImage.Width + 50;

            this.Left = (screenWidth / 2) - (this.Width / 2);
            this.Top = (screenHeight / 2) - (this.Height / 2);
        }

        private static async Task<TesseractOCR.Page> GetTesseractPageFromBitmap(Bitmap image)
        {
            // Image
            MemoryStream ms = new();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] fileBytes = ms.ToArray();

            // TesseractOCR
            var engine = new Engine(TesseractHelper.GetTessdataPath(), LanguageList.GetTesseractTagFromID(Setting.SourceLanguage), EngineMode.Default);
            var img = TesseractOCR.Pix.Image.LoadFromMemory(fileBytes);

            return engine.Process(img);
        }

        private static async Task<List<CaptureWordsEntrySimplify>> TesseractCaptureWordsySimplify(TesseractOCR.Page page)
        {
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

            return items;
        }

        private async Task AddToHistory(string original, List<CaptureWordsEntrySimplify> originalWords, string translated)
        {
            await HistoryLogger.Add(original, originalWords, translated, Setting.SourceLanguage, Setting.TargetLanguage);
        }

        private void ChangeCaptureImage(Bitmap bmp)
        {
            // If you get 'dllimport unknown'-, then add 'using System.Runtime.InteropServices;'
            [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool DeleteObject([In] IntPtr hObject);

            var handle = bmp.GetHbitmap();
            try
            {
                captureImage.Source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        // Paragraph
        private void Button_OriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(ocrText.Text, Setting.SourceLanguage);
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedText.Text, Setting.TargetLanguage);
        }

        // Word
        private async void Button_Word(object sender, RoutedEventArgs e)
        {

            Button? button = sender as Button;
            string word = button.ToolTip.ToString();

            if (string.IsNullOrWhiteSpace(word))
                return;

            // Change flyout position follow cursor
            var MousePos_Point = Mouse.GetPosition(originalCard);
            Matrix matrix = new Matrix();
            matrix.Translate(MousePos_Point.X - 50, MousePos_Point.Y);
            mt.Matrix = matrix;
            flayOut.LayoutTransform = Transform.Identity;

            flayOut.IsOpen = false;
            flayOut.IsOpen = true;
            definitionOriginal.Text = word;
            definitionTranslated.Text = "";
            definitionTranslatedLoading.Visibility = Visibility.Visible;

            TextToSpeech.StartTTS(definitionOriginal.Text, Setting.SourceLanguage);
            SavedWordButtonStateChange(word);

            if (!translatedCache.TryGetValue(word, out string translateResult))
            {
                translateResult = await LanguageList.TranslatedText(word, Setting.TargetLanguage);
                translatedCache.TryAdd(word, translateResult);
            }

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
            saveSymbolIcon?.Filled = await SavedWordLogger.IsExist(word);
        }

        private async void Button_WordSave(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(definitionTranslated.Text))
            {
                SnackbarHost.Show("Error", "Translation is not yet complete", "error", windows: "capture");
                return;
            }

            string word = definitionOriginal.Text;

            SavedWordLogger.ToggleSaved(word, definitionTranslated.Text, Setting.SourceLanguage, Setting.TargetLanguage);
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
