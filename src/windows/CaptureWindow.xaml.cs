using ScreenGrab;
using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using TesseractOCR;
using TesseractOCR.Enums;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;

namespace ScreenLookup.src.windows
{
    public partial class CaptureWindow : FluentWindow
    {
        private static CancellationTokenSource CTS;
        private bool IsCapturing = false;
        private readonly Dictionary<string, string> translatedCache = [];
        private DispatcherFrame ConfigDispatcher;
        private Engine TesseractEngine;
        private TesseractOCR.Page TesseractPage;

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
                {
                    if (flayOut.IsOpen)
                        flayOut.IsOpen = false;
                    else if (imageTranslatedExpander.IsExpanded)
                        CloseTranslatedExpanded();
                    else
                        HideWindow();
                }
            };
        }

        public void LoadInstalledLanguage()
        {
            List<ComboBoxItem> sourceItems = [];
            List<string> targetItems = [];

            for (int langID = 0; langID < TesseractHelper.LangList.Length - 1; langID++)
            {
                string languageTesseract = TesseractHelper.LangList[langID];
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{languageTesseract}";

                if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, langID))
                    sourceItems.Add(new ComboBoxItem
                    {
                        Content = $"{text}",
                        Tag = langID,
                    });

                targetItems.Add(text);
            }

            sourceLanguageConfig.ItemsSource = sourceItems;

            targetLanguageConfig.ItemsSource = targetItems;
            targetLanguageConfig.SelectedIndex = App.setting.TargetLanguage;
        }

        public void CreateTesseractEngine()
        {
            TesseractEngine?.Dispose();

            if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
                TesseractEngine = new(TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy), LanguageList.GetTesseractTagFromID(App.setting.SourceLanguage), EngineMode.Default);
        }

        private void HideWindow()
        {
            IsCapturing = false;
            ConfigDispatcher?.Continue = false;
            flayOut.IsOpen = false;
            translatedCache.Clear();
            TextToSpeech.StopTTS();
            CTS?.Cancel();

            if (TesseractPage != null && !TesseractPage.IsDisposed)
                TesseractPage.Dispose();

            this.Hide();
        }

        private void ShowWindow(bool IsConfig)
        {
            if (IsConfig)
            {
                configMenu.Visibility = Visibility.Visible;

                captureCard.Visibility = Visibility.Collapsed;
                originalCard.Visibility = Visibility.Collapsed;
                translatedCard.Visibility = Visibility.Collapsed;

                this.Width = 0;
                this.Topmost = true;
            }
            else
            {
                configMenu.Visibility = Visibility.Collapsed;

                if (App.setting.LookupOnImage)
                {
                    captureCard.Visibility = Visibility.Visible;
                    captureCardButton.Visibility = Visibility.Visible;
                    imageTranslatedExpander.Visibility = Visibility.Visible;
                    originalCard.Visibility = Visibility.Collapsed;
                    translatedCard.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (App.setting.ShowImage)
                        captureCard.Visibility = Visibility.Visible;
                    else
                        captureCard.Visibility = Visibility.Collapsed;
                    captureCardButton.Visibility = Visibility.Collapsed;
                    imageTranslatedExpander.Visibility = Visibility.Collapsed;
                    originalCard.Visibility = Visibility.Visible;
                    translatedCard.Visibility = Visibility.Visible;
                }

                this.Topmost = App.setting.Topmost;
            }

            this.Show();
            this.Activate();
        }


        public async void StartCaptureScreen()
        {
            if (IsCapturing)
                return;

            HideWindow();

            if (!TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
            {
                SnackbarHost.Show("Source Language", $"You have to download {LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)} in the setting", "error");
                Notification.Show($"You have to download {LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)} in the setting");
                return;
            }

            IsCapturing = true;
            CTS = new();
            ResetDefaultState();

            // Screenshot
            (Bitmap? image, bool isRightMouse, Point startPoint, Point endPoint) = ScreenGrabber.CaptureDialog(App.setting.ShowAuxiliary);
            if (image == null)
            {
                IsCapturing = false;
                return;
            }

            if (isRightMouse)
            {
                ConfigDispatcher = new DispatcherFrame();

                SelectSourceLanguageComboBox();
                CenterWindowOnScreen(new()
                {
                    X = endPoint.X - (this.ActualWidth / 2),
                    Y = endPoint.Y - (this.ActualHeight * 3.3),
                });
                ShowWindow(true);
                Dispatcher.PushFrame(ConfigDispatcher);

                if (!IsCapturing)
                    return;
            }

            ShowWindow(false);
            ChangeCaptureImage(image);
            if (App.setting.LookupOnImage)
                CenterWindowOnScreen(startPoint);
            else
                CenterWindowOnScreen();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    TesseractPage = await Task.Run(() => GetTesseractPageFromBitmap(image), CTS.Token);

                    if (IsCapturing)
                    {
                        if (string.IsNullOrWhiteSpace(TesseractPage.Text))
                        {
                            ResetDefaultState();
                            captureCard.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            // Original full paragraph
                            ocrText.Text = TesseractPage.Text;

                            // Original words card
                            List<CaptureWordsSimplifiedEntry> captureWords = await Task.Run(() => TesseractCaptureWordsySimplify(TesseractPage), CTS.Token);
                            if (IsCapturing)
                            {
                                if (App.setting.LookupOnImage)
                                    TesseractAltoText(TesseractPage);
                                else
                                {
                                    originalWords.ItemsSource = Convertor.ConvertCaptureWordsEntry(captureWords, App.setting.SourceLanguage, App.setting.TargetLanguage, this.Width);
                                    originalWordsLoading.Visibility = Visibility.Collapsed;
                                }

                                // Translate card
                                string translateResult = await Task.Run(() => Translation.GetTranslated(TesseractPage.Text, App.setting.TargetLanguage), CTS.Token);

                                if (App.setting.LookupOnImage)
                                {
                                    imageTranslatedText.Text = translateResult;
                                    imageTranslatedTextLoading.Visibility = Visibility.Collapsed;
                                }
                                else
                                {
                                    translatedText.Text = translateResult;
                                    translatedTextLoading.Visibility = Visibility.Collapsed;
                                }

                                if (IsCapturing)
                                {
                                    await AddToHistory(ocrText.Text, captureWords, translateResult);

                                    if (!App.setting.LookupOnImage)
                                        CenterWindowOnScreen();
                                }
                            }
                        }

                        IsCapturing = false;
                    }
                }));
            }, CTS.Token);
        }

        private void ResetDefaultState()
        {
            double buttonWidth = App.setting.FontSizeS + 10;
            double loadingWidth = App.setting.FontSizeS + 5;

            imageTranslatedText.FontSize = App.setting.FontSizeS;
            imageTranslatedText.FontFamily = new FontFamily(App.setting.FontFace);

            translatedText.FontSize = App.setting.FontSizeS;
            translatedText.FontFamily = new FontFamily(App.setting.FontFace);

            originalTTS.Width = buttonWidth;
            originalTTS.Height = buttonWidth;

            translatedTSS.Width = buttonWidth;
            translatedTSS.Height = buttonWidth;

            imageTranslatedTextLoading.Width = loadingWidth;
            imageTranslatedTextLoading.Height = loadingWidth;

            originalWordsLoading.Width = loadingWidth;
            originalWordsLoading.Height = loadingWidth;

            translatedTextLoading.Width = loadingWidth;
            translatedTextLoading.Height = loadingWidth;

            ocrCard.Visibility = Visibility.Collapsed;
            configMenu.Visibility = Visibility.Collapsed;
            captureCard.Visibility = Visibility.Collapsed;
            captureCardButton.Visibility = Visibility.Collapsed;
            imageTranslatedExpander.Visibility = Visibility.Collapsed;
            originalCard.Visibility = Visibility.Collapsed;
            translatedCard.Visibility = Visibility.Collapsed;

            imageTranslatedTextLoading.Visibility = Visibility.Visible;
            translatedTextLoading.Visibility = Visibility.Visible;
            originalWordsLoading.Visibility = Visibility.Visible;

            AltoText.ItemsSource = null;
            originalWords.ItemsSource = null;
            ocrText.Text = "";
            imageTranslatedText.Text = "";
            translatedText.Text = "";

            originalScrollView.ScrollToTop();
            translatedScrollViewer.ScrollToTop();
            imageTranslatedScrollViewer.ScrollToTop();

            CloseTranslatedExpanded();
        }

        private void CenterWindowOnScreen(Point gotoPoint = new())
        {
            double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
            double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

            if (!App.setting.LookupOnImage)
            {
                captureImage.Width = Math.Min(captureImage.Width, screenWidth);
                captureImage.Height = Math.Min(captureImage.Height, screenHeight / 2);
            }

            this.MaxWidth = screenWidth - 50;
            this.MaxHeight = screenHeight - 50;
            this.Width = Math.Min(this.MaxWidth, captureImage.Width + (App.setting.FontSizeS * 10));

            if (gotoPoint != new Point())
            {
                this.Left = gotoPoint.X;
                this.Top = gotoPoint.Y;
            }
            else
            {
                this.Left = (screenWidth / 2) - (this.ActualWidth / 2);
                this.Top = (screenHeight / 2) - (this.ActualHeight / 2);
            }
        }

        private async Task<TesseractOCR.Page> GetTesseractPageFromBitmap(Bitmap image)
        {
            // Image
            MemoryStream ms = new();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] fileBytes = ms.ToArray();

            // TesseractOCR
            var img = TesseractOCR.Pix.Image.LoadFromMemory(fileBytes);
            return TesseractEngine.Process(img);
        }

        private async Task<List<CaptureWordsSimplifiedEntry>> TesseractCaptureWordsySimplify(TesseractOCR.Page page)
        {
            List<CaptureWordsSimplifiedEntry> items = [];
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
                                if (!IsCapturing)
                                    goto skip;

                                string text = word.Text;
                                if (App.setting.HunSpell)
                                    text = HunspellHelper.CorrectionWord(text, App.setting.SourceLanguage);

                                items.Add(new CaptureWordsSimplifiedEntry() { Word = text, Stop = 0 });
                            }
                        }
                        items.Add(new CaptureWordsSimplifiedEntry() { Word = "", Stop = 1 });
                    }
                    items.Add(new CaptureWordsSimplifiedEntry() { Word = "", Stop = 2 });
                }
                items.Add(new CaptureWordsSimplifiedEntry() { Word = "", Stop = 3 });
            }

        skip:

            return items;
        }

        private async void TesseractAltoText(TesseractOCR.Page page)
        {
            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(page.AltoText);

            List<CaptureAltoEntry> items = [];

            foreach (XmlElement item in xmlDoc.GetElementsByTagName("ComposedBlock"))
            {
                string fullTextBlock = "";

                foreach (XmlElement textLine in item.GetElementsByTagName("TextBlock"))
                {
                    foreach (XmlElement data in textLine.GetElementsByTagName("String"))
                    {
                        if (!IsCapturing)
                            goto skip;

                        fullTextBlock += data.GetAttribute("CONTENT") + " ";
                    }
                }

                foreach (XmlElement textLine in item.GetElementsByTagName("TextBlock"))
                {
                    foreach (XmlElement data in textLine.GetElementsByTagName("String"))
                    {
                        if (!IsCapturing)
                            goto skip;

                        string Word = data.GetAttribute("CONTENT");
                        if (App.setting.HunSpell)
                            Word = HunspellHelper.CorrectionWord(Word, App.setting.SourceLanguage);

                        items.Add(new CaptureAltoEntry
                        {
                            Word = Word,
                            X = Int32.Parse(data.GetAttribute("HPOS")),
                            Y = Int32.Parse(data.GetAttribute("VPOS")) - 3,
                            Width = Int32.Parse(data.GetAttribute("WIDTH")) + 2,
                            Height = Int32.Parse(data.GetAttribute("HEIGHT")) + 5,
                            SourceLanguage = App.setting.sourceLanguage,
                            TargetLanguage = App.setting.TargetLanguage,
                            Uid = fullTextBlock,
                        });
                    }
                }
            }

        skip:

            AltoText.ItemsSource = items;
        }

        private async Task AddToHistory(string original, List<CaptureWordsSimplifiedEntry> originalWords, string translated)
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
                captureImage.Width = bmp.Width;
                captureImage.Height = bmp.Height;
            }
            finally { DeleteObject(handle); }
        }

        private void App_Deactivated(object sender, EventArgs e)
        {
            if (App.setting.CloseLostFocus)
                HideWindow();
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

        private void CloseTranslatedExpanded()
        {
            if (imageTranslatedExpander.IsExpanded)
                imageTranslatedExpander.IsExpanded = false;
        }

        #region button
        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            App.ToggleTopmost(!App.setting.Topmost);

            CloseTranslatedExpanded();
        }

        private async void Button_Word(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;
            string word = button.ToolTip.ToString();
            int sourceLang = Int32.Parse(button.Tag.ToString());

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.Show(word, "", sourceLang, App.setting.TargetLanguage);
        }

        private void Button_Paragraph(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;
            string word = button.ToolTip.ToString();
            string paragraph = button.Uid.ToString();
            int sourceLang = Int32.Parse(button.Tag.ToString());

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.Show(word, paragraph, sourceLang, App.setting.TargetLanguage);

            CloseTranslatedExpanded();
        }

        private void Button_OriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(ocrText.Text, App.setting.SourceLanguage, "capture");
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedText.Text, App.setting.TargetLanguage, "capture");
        }

        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false, windows: "capture");
        }

        private void captureWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseTranslatedExpanded();
        }
        #endregion

        #region configSection
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
        private void ConfigSubmit_Click(object sender, RoutedEventArgs e)
        {
            ConfigDispatcher.Continue = false;
        }

        private void ConfigSwitch_Toggle(object sender, RoutedEventArgs e)
        {
            ToggleSwitch switchs = (ToggleSwitch)sender;
            if (switchs.Name == "hunSpell")
                if (!HunspellHelper.IsInstalled(App.setting.SourceLanguage))
                    SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", "error", windows: "capture");
        }
        #endregion
    }
}
