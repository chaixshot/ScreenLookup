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
        private bool IsCapturing = false;
        private Engine TesseractEngine;
        private TesseractOCR.Page TesseractPage;

        private DispatcherFrame ConfigDispatcher;

        private int LastHistoryID;

        private Bitmap CapturedImage;
        private Bitmap CapturedImageEdited;

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

            if (App.setting.StartInBackground)
            {
                CreateTesseractEngine();
                SelectConfigLanguage();
            }

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

            captureWindow.Left = -10000;
            captureWindow.ShowWindow(true);
            captureWindow.HideWindow();
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
            flayOut.ClearCache();
            translationImage.Clear();
            translationMessage.Clear();
            translatedCache.Clear();
            TextToSpeech.StopTTS();
            ProcessImageCancelToken?.Cancel();
            TranslatesCancelToken?.Cancel();

            EditRotate = 0;
            EditZoom = 1.0;

            this.Left = -10000;

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
            ProcessImageCancelToken = new();
            TranslatesCancelToken = new();
            ResetDefaultState();

            // Screenshot
            (Bitmap? image, bool isRightMouse, Point startPoint, Point endPoint) = ScreenGrabber.CaptureDialog(App.setting.ShowAuxiliary);

            if (image == null)
            {
                IsCapturing = false;
                return;
            }

            CapturedImage = image;
            CapturedImageEdited = CapturedImage;

            if (isRightMouse)
            {
                ConfigDispatcher = new DispatcherFrame();

                SetWindowSize();
                SetWindowPosition(new()
                {
                    X = endPoint.X - (this.ActualWidth / 2),
                    Y = endPoint.Y - (this.ActualHeight * 2),
                });
                ShowWindow(true);
                Dispatcher.PushFrame(ConfigDispatcher);

                if (!IsCapturing)
                    return;
            }

            ShowWindow(false);
            ChangeCaptureImage(CapturedImageEdited);
            if (App.setting.LookupOnImage)
            {
                Point gotoPoint = endPoint;

                if (endPoint.X > startPoint.X)
                    gotoPoint.X -= endPoint.X - startPoint.X;
                if (endPoint.Y > startPoint.Y)
                    gotoPoint.Y -= endPoint.Y - startPoint.Y;

                SetWindowSize();
                SetWindowPosition(gotoPoint);
            }
            else
            {
                SetWindowSize();
                SetWindowPosition();
            }

            ProcessImage();
        }

        private void ProcessImage()
        {
            if (TesseractPage != null && !TesseractPage.IsDisposed)
                TesseractPage.Dispose();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                //Longer Process (//set the operation in another thread so that the UI thread is kept responding)
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    TesseractPage = await Task.Run(() => GetTesseractPageFromBitmap(CapturedImageEdited), ProcessImageCancelToken.Token);

                    if (IsCapturing)
                    {
                        if (string.IsNullOrWhiteSpace(TesseractPage.Text))
                        {
                            originalCard.Visibility = Visibility.Collapsed;
                            translatedCard.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            // Original full message
                            ocrText.Text = TesseractPage.Text;

                            // Original words card
                            List<CaptureWordsSimplifiedEntry> captureWords = await Task.Run(() => TesseractCaptureWordsySimplify(TesseractPage), ProcessImageCancelToken.Token);
                            if (IsCapturing)
                            {
                                LastHistoryID = await AddToHistory(ocrText.Text, captureWords);

                                if (App.setting.LookupOnImage)
                                    TesseractAltoText(TesseractPage);
                                else
                                {
                                    originalWords.ItemsSource = Convertor.ConvertCaptureWordsEntry(captureWords, App.setting.SourceLanguage, App.setting.TargetLanguage, this.Width);
                                    originalWordsLoading.Visibility = Visibility.Collapsed;
                                    originalCard.Visibility = Visibility.Visible;
                                    translatedCard.Visibility = Visibility.Visible;

                                    translationMessage.Set(TesseractPage.Text, App.setting.SourceLanguage, App.setting.TargetLanguage);
                                }

                                if (IsCapturing)
                                {
                                    if (!App.setting.LookupOnImage)
                                    {
                                        SetWindowSize();
                                        SetWindowPosition();
                                    }
                                }
                            }
                        }

                        IsCapturing = false;
                    }
                }));
            }, ProcessImageCancelToken.Token);
        }

        private async Task TranlsateImageExpander()
        {
            imageTranslatedExpanderContent.MinHeight = captureImage.Height;
            imageTranslatedExpanderContent.MaxHeight = captureImage.Height + (App.setting.FontSizeS * 3);

            await translationImage.Translate(TesseractPage.Text, App.setting.SourceLanguage, App.setting.TargetLanguage, TranslatesCancelToken);

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
            Contol_Undo.Visibility = Visibility.Collapsed;
            Contol_Confirm.Visibility = Visibility.Collapsed;

            AltoText.ItemsSource = null;
            originalWords.ItemsSource = null;
            ocrText.Text = "";

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

            this.MaxWidth = screenWidth - 50;
            this.MaxHeight = screenHeight - 50;
            this.Width = Math.Min(this.MaxWidth, captureImage.Width + (App.setting.FontSizeS * 10));
        }

        private void SetWindowPosition(Point gotoPoint = new())
        {
            double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
            double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

            Task.Delay(1).ContinueWith(_ => // Wait for Visible change fade effect
            {
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
                }));
            });
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
                                    text = HunspellHelper.CorrectionWord(text);

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
                            Word = HunspellHelper.CorrectionWord(Word);

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

        private async Task<int> AddToHistory(string original, List<CaptureWordsSimplifiedEntry> originalWords)
        {
            return await HistoryLogger.Add(original, originalWords, "", App.setting.SourceLanguage, App.setting.TargetLanguage);
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

            targetLanguageConfig.SelectedIndex = App.setting.targetLanguage;
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

            flayOut.Show(word, "", sourceLang, App.setting.TargetLanguage);
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
            TextToSpeech.StartTTS(ocrText.Text, App.setting.SourceLanguage, "capture");
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translationMessage.Translated, App.setting.TargetLanguage, "capture");
        }

        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

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

            CapturedImageEdited = CapturedImage;

            EditRotate = 0;
            EditZoom = 1.0;

            ChangeCaptureImage(CapturedImageEdited);
            SetWindowSize();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (CapturedImageEdited == CapturedImage)
                Contol_Undo.Visibility = Visibility.Collapsed;
            Contol_Confirm.Visibility = Visibility.Collapsed;

            IsCapturing = true;
            ProcessImage();
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
            CapturedImageEdited = CapturedImage;
            CapturedImageEdited = Convertor.BitmapRescale(CapturedImageEdited, EditZoom);
            CapturedImageEdited = Convertor.BitmapRotate(CapturedImageEdited, EditRotate);

            ChangeCaptureImage(CapturedImageEdited);
            SetWindowSize();
        }
        #endregion

        #region configSection
        private void SourceLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!App.Ready)
                return;

            var comboBox = sender as ComboBox;

            if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                App.setting.SourceLanguage = Int32.Parse(selectedItem.Tag.ToString());
        }

        private void TargetLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!App.Ready)
                return;

            var comboBox = sender as ComboBox;

            if (comboBox.SelectedItem is ComboBoxItem selectedItem)
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
                    SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", "error");
        }
        #endregion
    }
}
