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
using Point = System.Windows.Point;

namespace ScreenLookup.src.windows
{
    public partial class CaptureWindow : FluentWindow
    {
        private bool IsVR = false;
        private bool IsCapturing = false;
        private Engine TesseractEngine;
        private string TesseractPageText;

        private DispatcherFrame? ConfigDispatcher;

        private int LastHistoryID;

        private Bitmap CapturedImage;
        private Bitmap CapturedImageEditable;

        private static CancellationTokenSource ProcessImageCancelToken;
        private static CancellationTokenSource TranslatesCancelToken;

        private int EditRotate = 0;
        private double EditZoom = 1.0;

        private readonly Dictionary<string, string> translatedCache = [];

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
                    else if (imageTranslationExpander.IsExpanded)
                        CloseTranslatedExpanded();
                    else
                        HideWindow();
                }
            };

            imageTranslationExpander.Expanded += async (s, e) =>
            {
                TranlsateImageExpander();
            };
        }

        public void LoadInstalledLanguage()
        {
            List<ComboBoxItem> sourceItems = [];
            List<string> targetItems = [];

            for (int langID = 0; langID < TesseractHelper.LangList.Length - 1; langID++)
            {
                string tesseractTag = TesseractHelper.LangList[langID];
                string text = $"{LanguageList.GetDisplayNameFromTesseractTag(tesseractTag, true).PadRight(46)}\t{tesseractTag}";

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

            SelectConfigLanguage();
        }

        public void CreateTesseractEngine()
        {
            TesseractEngine?.Dispose();

            if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
                TesseractEngine = new(TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy), LanguageList.GetTesseractTagFromID(App.setting.SourceLanguage), EngineMode.Default);
        }

        public void HideWindow()
        {
            IsCapturing = false;
            ConfigDispatcher?.Continue = false;
            ConfigDispatcher = null;
            flayOut.IsOpen = false;
            flayOut.ClearCache();
            translationImage.Clear();
            translationMessage.Clear();
            translatedCache.Clear();
            TextToSpeech.StopTTS();
            ProcessImageCancelToken?.Cancel();
            TranslatesCancelToken?.Cancel();
            TesseractPageText = string.Empty;

            EditRotate = 0;
            EditZoom = 1.0;

            this.Left = -10000;

            this.Hide();
        }

        public void ShowWindow(bool IsConfig)
        {
            if (IsConfig)
            {
                configMenu.Visibility = Visibility.Visible;

                ImageControlPanel.Visibility = Visibility.Collapsed;

                captureCard.Visibility = Visibility.Collapsed;
                originalCard.Visibility = Visibility.Collapsed;
                translatedCard.Visibility = Visibility.Collapsed;

                this.Width = 0;
                this.Topmost = true;
            }
            else
            {
                configMenu.Visibility = Visibility.Collapsed;

                ImageControlPanel.Visibility = Visibility.Visible;

                if (App.setting.LookupOnImage)
                {
                    captureCard.Visibility = Visibility.Visible;
                    captureCardButton.Visibility = Visibility.Visible;
                    imageTranslationExpander.Visibility = Visibility.Visible;
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
                    imageTranslationExpander.Visibility = Visibility.Collapsed;
                    originalCard.Visibility = Visibility.Visible;
                    translatedCard.Visibility = Visibility.Visible;
                }

                this.Topmost = App.setting.Topmost;
            }

            this.Show();
            this.Activate();
        }

        public async void StartCaptureScreen(Bitmap? image = null, bool isRightMouse = false)
        {
            if (IsCapturing || ScreenGrabber.IsCapturing)
                return;

            if (!IsLoaded)
                ShowWindow(true);

            HideWindow();

            if (!TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
            {
                SnackbarHost.Show("Source Language", $"You have to download {LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)} in the setting", SnackbarType.Error);
                Notification.Show($"You have to download {LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)} in the setting");
                return;
            }

            IsCapturing = true;
            ProcessImageCancelToken = new();
            TranslatesCancelToken = new();
            ResetDefaultState();

            IsVR = image != null;

            Point startPoint;
            Point endPoint;

            // Desktop mode screenshot
            if (!IsVR)
            {
                AppUtilities.PlaySound("ready.wav");
                var captureResult = ScreenGrabber.CaptureDialog(App.setting.ShowAuxiliary);
                if (captureResult == null)
                {
                    IsCapturing = false;
                    return;
                }

                (image, isRightMouse, startPoint, endPoint) = captureResult;

                if (image == null)
                {
                    IsCapturing = false;
                    return;
                }
                AppUtilities.PlaySound("screenshot.wav");
            }

            CapturedImage = IsVR ? SetBitmapDimension(image) : image;
            CapturedImageEditable = CapturedImage;

            // Option mode
            if (isRightMouse)
            {
                ConfigDispatcher = new DispatcherFrame();

                DispatcherFrame cache = ConfigDispatcher;

                SetWindowSize();
                if (IsVR)
                    SetWindowPosition();
                else
                    SetWindowPosition(new()
                    {
                        X = endPoint.X - (this.ActualWidth / 2),
                        Y = endPoint.Y - (this.ActualHeight * 2),
                    });
                ShowWindow(true);

                Dispatcher.PushFrame(ConfigDispatcher);

                if (cache != ConfigDispatcher)
                    return;
            }

            ShowWindow(false);
            ChangeCaptureImage(CapturedImageEditable);

            if (App.setting.LookupOnImage)
            {
                SetWindowSize();

                if (IsVR)
                    SetWindowPosition();
                else
                {
                    Point gotoPoint = endPoint;

                    if (endPoint.X > startPoint.X)
                        gotoPoint.X -= endPoint.X - startPoint.X;
                    if (endPoint.Y > startPoint.Y)
                        gotoPoint.Y -= endPoint.Y - startPoint.Y;

                    SetWindowPosition(gotoPoint);
                }
            }
            else
            {
                SetWindowSize();
                SetWindowPosition();
            }

            ProcessImage(CapturedImageEditable);
        }

        private void ProcessImage(Bitmap image)
        {
            UpdateOverlayScale();
            ProcessImageOverlay.Visibility = Visibility.Visible;

            Task.Run(async () =>
            {
                try
                {
                    using var tesseractPage = await GetTesseractPageFromBitmap(image).ConfigureAwait(false);
                    var pageText = tesseractPage.Text;
                    var captureWords = TesseractCaptureWordsySimplify(tesseractPage);
                    // Offload heavy XML parsing to background thread
                    var altoEntries = App.setting.LookupOnImage ? TesseractAltoTextProcess(tesseractPage) : null;

                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsCapturing) return;
                        TesseractPageText = pageText;

                        if (string.IsNullOrWhiteSpace(TesseractPageText))
                        {
                            originalCard.Visibility = Visibility.Collapsed;
                            translatedCard.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            ocrText.Text = TesseractPageText;
                            LastHistoryID = await AddToHistory(ocrText.Text, captureWords);

                            if (App.setting.LookupOnImage)
                                AltoText.ItemsSource = altoEntries;
                            else
                            {
                                originalWords.ItemsSource = Convertor.ConvertCaptureWordsEntry(captureWords, App.setting.SourceLanguage, App.setting.TargetLanguage, this.Width);
                                originalWordsLoading.Visibility = Visibility.Collapsed;
                                originalCard.Visibility = Visibility.Visible;
                                translatedCard.Visibility = Visibility.Visible;
                                translationMessage.Set(TesseractPageText, App.setting.SourceLanguage, App.setting.TargetLanguage);
                            }

                            if (!App.setting.LookupOnImage)
                            {
                                SetWindowSize();
                                SetWindowPosition();
                            }
                        }
                        IsCapturing = false;
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OCR Error: {ex.Message}"); }
                finally
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ProcessImageOverlay.Visibility = Visibility.Collapsed;
                    });
                }
            }, ProcessImageCancelToken.Token);
        }

        private static Bitmap SetBitmapDimension(Bitmap bmp)
        {
            int canvasWidth = 1080;
            int canvasHeight = 720;

            // Calculate the scaling ratio to fit inside the box perfectly
            double ratioX = (double)canvasWidth / bmp.Width;
            double ratioY = (double)canvasHeight / bmp.Height;
            double ratio = Math.Min(ratioX, ratioY);

            // Compute the size of the scaled image
            int newWidth = (int)(bmp.Width * ratio);
            int newHeight = (int)(bmp.Height * ratio);

            if (newWidth < 1) newWidth = 1;
            if (newHeight < 1) newHeight = 1;

            int posX = (canvasWidth - newWidth) / 2;
            int posY = (canvasHeight - newHeight) / 2;

            Bitmap resizedBmp = new Bitmap(canvasWidth, canvasHeight);

            using (Graphics g = Graphics.FromImage(resizedBmp))
            {
                g.Clear(Color.White);

                // Render flags for professional, sharp downscaling quality
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                g.DrawImage(bmp, posX, posY, newWidth, newHeight);
            }

            return resizedBmp;
        }

        private async Task TranlsateImageExpander()
        {
            imageTranslatedExpanderContent.MinHeight = captureImage.Height;
            imageTranslatedExpanderContent.MaxHeight = captureImage.Height + (App.setting.FontSizeS * 3);

            await translationImage.Translate(TesseractPageText, App.setting.SourceLanguage, App.setting.TargetLanguage, TranslatesCancelToken);

            HistoryLogger.Update(LastHistoryID, translationImage.Translated);
        }

        private void ResetDefaultState()
        {
            double buttonWidth = App.setting.FontSizeS + 10;
            double loadingWidth = App.setting.FontSizeS + 5;

            originalTTS.Width = buttonWidth;
            originalTTS.Height = buttonWidth;

            translatedTSS.Width = buttonWidth;
            translatedTSS.Height = buttonWidth;

            originalWordsLoading.Width = loadingWidth;
            originalWordsLoading.Height = loadingWidth;

            ocrCard.Visibility = Visibility.Collapsed;
            configMenu.Visibility = Visibility.Collapsed;
            captureCard.Visibility = Visibility.Collapsed;
            captureCardButton.Visibility = Visibility.Collapsed;
            imageTranslationExpander.Visibility = Visibility.Collapsed;
            originalCard.Visibility = Visibility.Collapsed;
            translatedCard.Visibility = Visibility.Collapsed;

            originalWordsLoading.Visibility = Visibility.Visible;
            ProcessImageOverlay.Visibility = Visibility.Collapsed;
            Contol_Undo.Visibility = Visibility.Collapsed;
            Contol_Confirm.Visibility = Visibility.Collapsed;

            AltoText.ItemsSource = null;
            originalWords.ItemsSource = null;
            ocrText.Text = string.Empty;

            originalScrollView.ScrollToTop();

            Grid.SetRow(flayOut, App.setting.LookupOnImage ? 1 : 3);

            CloseTranslatedExpanded();
        }

        private void SetWindowSize()
        {
            double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
            double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

            if (!App.setting.LookupOnImage)
            {
                captureImage.Width = Math.Min(captureImage.Width, screenWidth);
                captureImage.Height = Math.Min(captureImage.Height, screenHeight / 2);
            }

            UpdateOverlayScale();

            this.MaxWidth = screenWidth - 50;
            this.MaxHeight = screenHeight - 50;
            this.Width = Math.Min(this.MaxWidth, captureImage.Width + (App.setting.FontSizeS * 10));
        }

        private void UpdateOverlayScale()
        {
            if (captureImage.Width > 0 && captureImage.Height > 0)
            {
                ProcessImageOverlayViewbox.MaxHeight = Math.Max(20, captureImage.Height * 0.35);
                ProcessImageOverlayViewbox.MaxWidth = Math.Max(60, captureImage.Width * 0.85);
            }
        }

        private void SetWindowPosition(Point gotoPoint = new())
        {
            double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
            double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

            // Use priority Render to move after the current layout pass without a fixed Task.Delay
            Dispatcher.BeginInvoke(new Action(() =>
            {
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

                double maxLeft = screenWidth - this.ActualWidth;
                double maxTop = screenHeight - this.ActualHeight;

                this.Left = Math.Max(Math.Min(this.Left, maxLeft), 0);
                this.Top = Math.Max(Math.Min(this.Top, maxTop), 0);
            }), DispatcherPriority.Render);
        }

        private async Task<TesseractOCR.Page> GetTesseractPageFromBitmap(Bitmap image)
        {
            using MemoryStream ms = new();
            // BMP is significantly faster to encode than PNG for internal memory transfers
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] fileBytes = ms.ToArray();

            TesseractOCR.Pix.Image img = TesseractOCR.Pix.Image.LoadFromMemory(fileBytes);
            return TesseractEngine.Process(img);
        }

        private List<CaptureWordsSimplifiedEntry> TesseractCaptureWordsySimplify(TesseractOCR.Page page)
        {
            List<CaptureWordsSimplifiedEntry> items = [];
            foreach (TesseractOCR.Layout.Block block in page.Layout)
            {
                foreach (TesseractOCR.Layout.Paragraph paragraph in block.Paragraphs)
                {
                    foreach (TesseractOCR.Layout.TextLine textLine in paragraph.TextLines)
                    {
                        foreach (TesseractOCR.Layout.Word word in textLine.Words)
                        {
                            string text = word.Text;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (App.setting.HunSpell)
                                    text = HunspellHelper.CorrectionWord(text);

                                items.Add(new CaptureWordsSimplifiedEntry() { Word = text, Stop = 0 });
                            }
                        }
                        items.Add(new CaptureWordsSimplifiedEntry() { Word = string.Empty, Stop = 1 });
                    }
                    items.Add(new CaptureWordsSimplifiedEntry() { Word = string.Empty, Stop = 2 });
                }
                items.Add(new CaptureWordsSimplifiedEntry() { Word = string.Empty, Stop = 3 });
            }

            return items;
        }

        private static bool IsCjk(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) || // CJK Unified Ideographs
                   (c >= 0x3040 && c <= 0x309F) || // Hiragana
                   (c >= 0x30A0 && c <= 0x30FF) || // Katakana
                   (c >= 0x3400 && c <= 0x4DBF) || // CJK Extension A
                   (c >= 0xAC00 && c <= 0xD7AF) || // Hangul Syllables
                   (c >= 0x3000 && c <= 0x303F) || // CJK Symbols and Punctuation
                   (c >= 0xFF00 && c <= 0xFFEF);   // Halfwidth and Fullwidth Forms
        }

        private List<CaptureAltoEntry> TesseractAltoTextProcess(TesseractOCR.Page page)
        {
            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(page.AltoText);
            List<CaptureAltoEntry> items = [];

            var textBlocks = xmlDoc.GetElementsByTagName("TextBlock");
            foreach (XmlElement textBlock in textBlocks)
            {
                // Build fullTextBlock for the entire TextBlock (Uid / translation context)
                var allStrings = textBlock.GetElementsByTagName("String");
                System.Text.StringBuilder fullTextBuilder = new();
                string prevContent = string.Empty;

                foreach (XmlElement data in allStrings)
                {
                    string content = data.GetAttribute("CONTENT");
                    if (string.IsNullOrEmpty(content)) continue;

                    if (fullTextBuilder.Length > 0 && !string.IsNullOrEmpty(prevContent))
                    {
                        char lastChar = prevContent[^1];
                        char firstChar = content[0];
                        if (!IsCjk(lastChar) || !IsCjk(firstChar))
                            fullTextBuilder.Append(' ');
                    }
                    fullTextBuilder.Append(content);
                    prevContent = content;
                }
                string fullTextBlock = fullTextBuilder.ToString();

                var textLines = textBlock.GetElementsByTagName("TextLine");
                if (textLines.Count == 0)
                {
                    // Fallback if no TextLine tags exist in ALTO
                    foreach (XmlElement data in allStrings)
                    {
                        string word = data.GetAttribute("CONTENT");
                        if (string.IsNullOrWhiteSpace(word)) continue;

                        if (App.setting.HunSpell)
                            word = HunspellHelper.CorrectionWord(word);

                        items.Add(new CaptureAltoEntry
                        {
                            Word = word,
                            X = int.TryParse(data.GetAttribute("HPOS"), out int x) ? x : 0,
                            Y = Math.Max(0, (int.TryParse(data.GetAttribute("VPOS"), out int y) ? y : 0) - 2),
                            Width = Math.Max(4, (int.TryParse(data.GetAttribute("WIDTH"), out int w) ? w : 0) + 2),
                            Height = Math.Max(8, (int.TryParse(data.GetAttribute("HEIGHT"), out int h) ? h : 0) + 4),
                            SourceLanguage = App.setting.SourceLanguage,
                            TargetLanguage = App.setting.TargetLanguage,
                            Uid = fullTextBlock,
                        });
                    }
                    continue;
                }

                foreach (XmlElement textLine in textLines)
                {
                    int lineX = int.TryParse(textLine.GetAttribute("HPOS"), out int lx) ? lx : 0;
                    int lineY = int.TryParse(textLine.GetAttribute("VPOS"), out int ly) ? ly : 0;
                    int lineWidth = int.TryParse(textLine.GetAttribute("WIDTH"), out int lw) ? lw : 0;
                    int lineHeight = int.TryParse(textLine.GetAttribute("HEIGHT"), out int lh) ? lh : 0;
                    bool isVertical = lineHeight > lineWidth && lineWidth > 0;

                    var lineStrings = textLine.GetElementsByTagName("String");
                    for (int i = 0; i < lineStrings.Count; i++)
                    {
                        XmlElement data = (XmlElement)lineStrings[i];
                        string word = data.GetAttribute("CONTENT");
                        if (string.IsNullOrWhiteSpace(word)) continue;

                        if (App.setting.HunSpell)
                            word = HunspellHelper.CorrectionWord(word);

                        int strX = int.TryParse(data.GetAttribute("HPOS"), out int sx) ? sx : lineX;
                        int strY = int.TryParse(data.GetAttribute("VPOS"), out int sy) ? sy : lineY;
                        int strW = int.TryParse(data.GetAttribute("WIDTH"), out int sw) ? sw : 0;
                        int strH = int.TryParse(data.GetAttribute("HEIGHT"), out int sh) ? sh : 0;

                        double x, y, width, height;

                        if (!isVertical)
                        {
                            // Horizontal text line:
                            // Anchor Y and Height to the TextLine bounds so individual characters/punctuation
                            // with unstable or exaggerated VPOS/HEIGHT don't overflow vertically into lines below.
                            y = Math.Max(0, lineY - 2);
                            height = Math.Max(8, lineHeight + 4);

                            x = Math.Max(0, strX);

                            // Clamp width so adjacent items on the same line never overlap
                            int availableW = strW + 2;
                            if (i + 1 < lineStrings.Count)
                            {
                                XmlElement nextData = (XmlElement)lineStrings[i + 1];
                                if (int.TryParse(nextData.GetAttribute("HPOS"), out int nextX) && nextX > strX)
                                {
                                    availableW = Math.Min(availableW, nextX - strX);
                                }
                            }
                            width = Math.Max(4, availableW);
                        }
                        else
                        {
                            // Vertical text line:
                            // Anchor X and Width to the column bounds, allowing Y and Height to flow vertically.
                            x = Math.Max(0, lineX - 2);
                            width = Math.Max(8, lineWidth + 4);

                            y = Math.Max(0, strY);

                            int availableH = strH + 2;
                            if (i + 1 < lineStrings.Count)
                            {
                                XmlElement nextData = (XmlElement)lineStrings[i + 1];
                                if (int.TryParse(nextData.GetAttribute("VPOS"), out int nextY) && nextY > strY)
                                {
                                    availableH = Math.Min(availableH, nextY - strY);
                                }
                            }
                            height = Math.Max(4, availableH);
                        }

                        items.Add(new CaptureAltoEntry
                        {
                            Word = word,
                            X = x,
                            Y = y,
                            Width = width,
                            Height = height,
                            SourceLanguage = App.setting.SourceLanguage,
                            TargetLanguage = App.setting.TargetLanguage,
                            Uid = fullTextBlock,
                        });
                    }
                }
            }
            return items;
        }

        private async Task<int> AddToHistory(string original, List<CaptureWordsSimplifiedEntry> originalWords)
        {
            return await HistoryLogger.Add(original, originalWords, string.Empty, App.setting.SourceLanguage, App.setting.TargetLanguage);
        }

        private void ChangeCaptureImage(Bitmap bmp)
        {
            // If you get 'dllimport unknown'-, then add 'using System.Runtime.InteropServices;'
            [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool DeleteObject([In] IntPtr hObject);

            nint handle = bmp.GetHbitmap();
            try
            {
                captureImage.Source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                captureImage.Width = bmp.Width;
                captureImage.Height = bmp.Height;
                UpdateOverlayScale();
            }
            finally { DeleteObject(handle); }
        }

        private void App_Deactivated(object sender, EventArgs e)
        {
            if (App.setting.CloseLostFocus)
                HideWindow();
        }

        public void SelectConfigLanguage()
        {
            foreach (ComboBoxItem item in sourceLanguageConfig.Items)
            {
                if (Int32.Parse(item.Tag.ToString()) == App.setting.SourceLanguage)
                {
                    sourceLanguageConfig.SelectedItem = item;
                    break;
                }
            }

            targetLanguageConfig.SelectedIndex = App.setting.TargetLanguage;
        }

        private void CloseTranslatedExpanded()
        {
            if (imageTranslationExpander.IsExpanded)
                imageTranslationExpander.IsExpanded = false;
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

            flayOut.Show(word, string.Empty, sourceLang, App.setting.TargetLanguage);
        }

        private void Button_Message(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;
            string word = button.ToolTip.ToString();
            string message = button.Uid.ToString();
            int sourceLang = Int32.Parse(button.Tag.ToString());

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.Show(word, message, sourceLang, App.setting.TargetLanguage);

            CloseTranslatedExpanded();
        }

        private void Button_OriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(ocrText.Text, App.setting.SourceLanguage);
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translationMessage.Translated, App.setting.TargetLanguage);
        }

        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;

            Clipboard.SetText(button.Tag.ToString());
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
        }

        private void captureWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseTranslatedExpanded();
        }
        #endregion

        #region Capture edit control panel
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Contol_Undo.Visibility = Visibility.Hidden;
            Contol_Confirm.Visibility = Visibility.Visible;

            CapturedImageEditable = CapturedImage;

            EditRotate = 0;
            EditZoom = 1.0;

            ChangeCaptureImage(CapturedImageEditable);
            SetWindowSize();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (CapturedImageEditable == CapturedImage)
                Contol_Undo.Visibility = Visibility.Collapsed;
            Contol_Confirm.Visibility = Visibility.Collapsed;

            IsCapturing = true;
            ProcessImage(CapturedImageEditable);
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            AltoText.ItemsSource = null;

            Contol_Undo.Visibility = Visibility.Visible;
            Contol_Confirm.Visibility = Visibility.Visible;

            EditRotate -= 1;
            ApplyCaptureEdit();
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            AltoText.ItemsSource = null;

            Contol_Undo.Visibility = Visibility.Visible;
            Contol_Confirm.Visibility = Visibility.Visible;

            EditRotate += 1;
            ApplyCaptureEdit();
        }

        private void Zoom_Click(object sender, RoutedEventArgs e)
        {
            AltoText.ItemsSource = null;

            Contol_Undo.Visibility = Visibility.Visible;
            Contol_Confirm.Visibility = Visibility.Visible;

            EditZoom += 0.1;
            ApplyCaptureEdit();
        }

        private void ApplyCaptureEdit()
        {
            CapturedImageEditable = CapturedImage;
            CapturedImageEditable = Convertor.BitmapRescale(CapturedImageEditable, EditZoom);
            CapturedImageEditable = Convertor.BitmapRotate(CapturedImageEditable, EditRotate);

            ChangeCaptureImage(CapturedImageEditable);
            SetWindowSize();
            SetWindowPosition(new Point(this.Left, this.Top));
        }
        #endregion

        #region configSection
        private void SourceLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox? comboBox = sender as ComboBox;

            if (comboBox.IsDropDownOpen)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                    App.setting.SourceLanguage = Int32.Parse(selectedItem.Tag.ToString());
            }
        }

        private void TargetLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox? comboBox = sender as ComboBox;

            if (comboBox.IsDropDownOpen)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                    App.setting.TargetLanguage = Int32.Parse(selectedItem.Tag.ToString());
            }
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
                    SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", SnackbarType.Error);
        }
        #endregion
    }
}
