using HunspellSharp;
using ScreenGrab;
using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TesseractOCR;
using TesseractOCR.Enums;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;

namespace ScreenLookup.src.windows
{
    public partial class CaptureWindow : FluentWindow
    {
        private bool IsCapturing = false;
        private readonly Dictionary<string, string> translatedCache = [];
        private DispatcherFrame configDispatcher;

        public CaptureWindow()
        {
            DataContext = App.setting;
            InitializeComponent();
            ResetDefaultState();
            LoadInstalledLanguage();

            Loaded += (s, e) =>
            {
                ApplicationThemeManager.ApplySystemTheme();
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    HideWindow();
            };
        }

        public void LoadInstalledLanguage()
        {
            sourceLanguageConfig.Items.Clear();
            targetLanguageConfig.Items.Clear();

            for (int langID = 0; langID < LanguageList.LanguageTesseract.Length - 1; langID++)
            {
                string languageTesseract = LanguageList.LanguageTesseract[langID];
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{languageTesseract}";

                // sourceLanguageConfig
                if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, langID))
                    sourceLanguageConfig.Items.Add(new ComboBoxItem
                    {
                        Content = $"{text}",
                        Tag = langID,
                    });

                // targetLanguageConfig
                targetLanguageConfig.Items.Add(text);
            }

            targetLanguageConfig.SelectedIndex = App.setting.TargetLanguage;
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

        private void ToggleConfigMenu(bool show)
        {
            if (show)
            {
                configSection.Visibility = Visibility.Visible;
                resultSection.Visibility = Visibility.Collapsed;

                System.Drawing.Point point = System.Windows.Forms.Control.MousePosition;
                var transform = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;
                var mouse = transform.Transform(new System.Windows.Point(point.X, point.Y));

                this.Left = mouse.X - (this.ActualWidth / 2);
                this.Top = mouse.Y - (this.ActualHeight / 2);
                this.Width = 0;
            }
            else
            {
                configSection.Visibility = Visibility.Collapsed;
                resultSection.Visibility = Visibility.Visible;
            }
        }

        public async void StartCaptureScreen()
        {
            if (IsCapturing)
                return;

            HideWindow();

            if (!TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
            {
                Notification.Show($"You have to install {LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)} in the setting");
                return;
            }

            // Screenshot
            IsCapturing = true;
            (Bitmap? image, bool isRightMouse) = ScreenGrabber.CaptureDialog(false);
            IsCapturing = false;
            if (image == null)
            {
                return;
            }

            ResetDefaultState();

            if (isRightMouse)
            {
                configDispatcher = new DispatcherFrame();

                SelectSourceLanguageComboBox();
                ShowWindow();
                ToggleConfigMenu(true);

                Dispatcher.PushFrame(configDispatcher);
            }

            ToggleConfigMenu(false);
            ChangeCaptureImage(image);
            CenterWindowOnScreen(image.Width, image.Height);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    TesseractOCR.Page tesseract = await GetTesseractPageFromBitmap(image);

                    if (string.IsNullOrWhiteSpace(tesseract.Text))
                    {
                        originalCard.Visibility = Visibility.Collapsed;
                        translatedCard.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // Original full paragraph
                        ocrText.Text = tesseract.Text;

                        // Original words card
                        List<CaptureWordsEntrySimplify> captureWords = await TesseractCaptureWordsySimplify(tesseract);
                        originalWords.ItemsSource = Convertor.ConvertCaptureWordsEntry(captureWords, width: this.Width);
                        originalWordsLoading.Visibility = Visibility.Collapsed;

                        // Translate card
                        string translateResult = await LanguageList.TranslatedText(tesseract.Text, App.setting.TargetLanguage);
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
            int buttonWidth = App.setting.FontSizeS + 10;
            ocrText.Text = "";
            ocrCard.Visibility = Visibility.Collapsed;

            translatedText.FontSize = App.setting.FontSizeS;
            translatedText.FontFamily = new FontFamily(App.setting.FontFace);

            definitionOriginal.FontSize = App.setting.FontSizeS;
            definitionOriginal.FontFamily = new FontFamily(App.setting.FontFace);

            definitionTranslated.FontSize = App.setting.FontSizeS;
            definitionTranslated.FontFamily = new FontFamily(App.setting.FontFace);

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

            captureCard.Visibility = App.setting.ShowImage ? Visibility.Visible : Visibility.Collapsed;

            translatedTextLoading.Visibility = Visibility.Visible;
            originalWordsLoading.Visibility = Visibility.Visible;

            originalCard.Visibility = Visibility.Visible;
            translatedCard.Visibility = Visibility.Visible;

            originalWords.ItemsSource = null;
            translatedText.Text = "";

            this.Topmost = App.setting.Topmost;
        }

        private void CenterWindowOnScreen(double imgWidth, double imgHeight)
        {
            double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
            double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

            captureImage.Width = Math.Min(imgWidth, screenWidth);
            captureImage.Height = Math.Min(imgHeight, screenHeight / 2);

            this.MaxWidth = screenWidth - 100;
            this.MaxHeight = screenHeight - 100;
            this.Width = captureImage.Width + (App.setting.FontSizeS * 10);

            this.Left = (screenWidth / 2) - (this.ActualWidth / 2);
            this.Top = (screenHeight / 2) - (this.ActualHeight / 2);
        }

        private static async Task<TesseractOCR.Page> GetTesseractPageFromBitmap(Bitmap image)
        {
            // Image
            MemoryStream ms = new();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] fileBytes = ms.ToArray();

            // TesseractOCR
            var engine = new Engine(TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy), LanguageList.GetTesseractTagFromID(App.setting.SourceLanguage), EngineMode.Default);
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

                                if (App.setting.HunSpell)
                                {
                                    string DisplayName = LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, false);
                                    if (HunspellHelper.FileNames.TryGetValue(DisplayName, out string? fileName))
                                    {
                                        string nameTag = fileName.Split('/')[1];
                                        var hunspell = new Hunspell($"{HunspellHelper.FilePath}\\{nameTag}.aff", $"{HunspellHelper.FilePath}\\{nameTag}.dic");
                                        if (!hunspell.Spell(word.Text))
                                        {
                                            List<string> suggestions = hunspell.Suggest(word.Text);
                                            if (suggestions.Count != 0)
                                                text = suggestions[0];
                                        }
                                    }
                                    else
                                    {
                                        SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\" dosen't support Hunspell", "error", windows: "capture");
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
            await HistoryLogger.Add(original, originalWords, translated, App.setting.SourceLanguage, App.setting.TargetLanguage);
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
            TextToSpeech.StartTTS(ocrText.Text, App.setting.SourceLanguage);
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedText.Text, App.setting.TargetLanguage);
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

            TextToSpeech.StartTTS(definitionOriginal.Text, App.setting.SourceLanguage);
            SavedWordButtonStateChange(word);

            if (!translatedCache.TryGetValue(word, out string translateResult))
            {
                translateResult = await LanguageList.TranslatedText(word, App.setting.TargetLanguage);
                translatedCache.TryAdd(word, translateResult);
            }

            definitionTranslated.Text = translateResult;
            definitionTranslatedLoading.Visibility = Visibility.Collapsed;
        }

        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(definitionOriginal.Text, App.setting.SourceLanguage);
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(definitionTranslated.Text, App.setting.TargetLanguage);
        }

        private void Button_OpenBrowser(object sender, RoutedEventArgs e)
        {
            switch (App.setting.translationProvider)
            {
                case 4:
                    Process.Start(new ProcessStartInfo($"https://translate.yandex.com/en/?source_lang={LanguageList.GetLanguageISO6391FromID(App.setting.SourceLanguage)}&target_lang={LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage)}&text={definitionOriginal.Text}") { UseShellExecute = true });

                    break;
                default:
                    Process.Start(new ProcessStartInfo($"https://translate.google.com/?sl={LanguageList.GetLanguageISO6391FromID(App.setting.SourceLanguage)}&tl={LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage)}&text={definitionOriginal.Text}&op=translate") { UseShellExecute = true });
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

            SavedWordLogger.ToggleSaved(word, definitionTranslated.Text, App.setting.SourceLanguage, App.setting.TargetLanguage);
            SavedWordButtonStateChange(word);
        }

        // Utility
        private void App_Deactivated(object sender, EventArgs e)
        {
            if (App.setting.CloseLostFocus)
                HideWindow();
        }

        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false, windows: "capture");
        }

        private void SourceLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedItem = comboBox.SelectedItem as ComboBoxItem;

            if (selectedItem != null)
                App.setting.SourceLanguage = Int32.Parse(selectedItem.Tag.ToString());
        }

        private void TargetLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedItem = comboBox.SelectedItem as ComboBoxItem;

            if (selectedItem != null)
                App.setting.TargetLanguage = Int32.Parse(selectedItem.Tag.ToString());
        }

        public void SelectSourceLanguageComboBox()
        {
            foreach (ComboBoxItem item in sourceLanguageConfig.Items)
            {
                if (Int32.Parse(item.Tag.ToString()) == App.setting.SourceLanguage)
                {
                    sourceLanguageConfig.SelectedItem = item;
                    break;
                }
            }

            targetLanguageConfig.SelectedIndex = App.setting.targetLanguage;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            configDispatcher.Continue = false;
        }

        private void ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (!HunspellHelper.IsInstalled(App.setting.SourceLanguage))
                SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", "error", windows: "capture");
        }
    }
}
