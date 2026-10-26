using ScreenGrab;
using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using ScreenLookup.src.utils.Database;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TesseractOCR.Enums;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using Point = System.Windows.Point;

namespace ScreenLookup.src.windows
{
    public partial class CaptureWindow : FluentWindow
    {
        #region Native Imports
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject([In] IntPtr hObject);
        #endregion

        #region Fields & Properties
        private bool IsVR = false;
        private bool IsCapturing = false;
        private TesseractOCR.Engine? TesseractEngine;
        private string TesseractPageText = string.Empty;

        private DispatcherFrame? ConfigDispatcher;
        private int LastHistoryID;

        private Bitmap? CapturedImage;
        private Bitmap? CapturedImageEditable;

        private static CancellationTokenSource ProcessImageCancelToken = new();
        private static CancellationTokenSource TranslatesCancelToken = new();

        private bool isImageEditing = false;
        private bool isDraggingRotation = false;
        private bool isDraggingScaling = false;
        private float editRotate = 0;
        private double editZoom = 1.0;
        private Point rotationDragStartPoint;
        private float rotationDragStartAngle = 0;
        private Point scalingDragStartPoint;
        private double scalingDragStartZoom = 1.0;

        private readonly Dictionary<string, string> translatedCache = [];
        #endregion

        #region Constructor & Initialization
        public CaptureWindow()
        {
            DataContext = App.setting;
            InitializeComponent();
            ResetDefaultState();

            Loaded += (s, e) =>
            {
                ApplicationThemeManager.ApplySystemTheme();
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);

                LoadInstalledLanguage();
                CreateTesseractEngine();
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    if (flayOut.IsOpen)
                        flayOut.IsOpen = false;
                    else if (translationExpander.IsExpanded)
                        CloseTranslatedExpanded();
                    else
                        CloseWindow();
                }
            };

            translationExpander.Expanded += async (s, e) =>
            {
                await TranlsateImageExpander();
            };

            translationExpander.Collapsed += async (s, e) =>
            {
                TextToSpeech.StopTTS();
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
            TesseractEngine = null;

            if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
            {
                TesseractEngine = new TesseractOCR.Engine(
                    TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy),
                    LanguageList.GetTesseractTagFromID(App.setting.SourceLanguage),
                    EngineMode.Default);
            }
        }
        #endregion

        #region Capture Operations
        public void DesktopCaptureScreen()
        {
            try
            {
                if (IsCapturing || ScreenGrabber.IsCapturing)
                    return;

                if (!TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
                {
                    SnackbarHost.Show("Source Language", $"You have to download {LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)} in the setting", SnackbarType.Error);
                    Notification.Show($"You have to download {LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)} in the setting");
                    return;
                }

                IsCapturing = true;
                AppUtilities.PlaySound("ready.wav");

                var captureResult = ScreenGrabber.CaptureDialog(App.setting.ShowAuxiliary);
                if (captureResult != null)
                {
                    var (image, clickState, startPoint, endPoint) = captureResult;

                    if (clickState == 2) // Middle Click - Show last result
                        OpenWindow();
                    else
                    {
                        AppUtilities.PlaySound("screenshot.wav");
                        PrepairCaptureWindow(image, clickState == 1, startPoint, endPoint);
                    }
                }
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("CaptureWindow", $"DesktopCaptureScreen Error: {ex.Message}", type: SnackbarType.Error);
            }
            finally
            {
                IsCapturing = false;
            }
        }

        public void PrepairCaptureWindow(Bitmap? image, bool showOption, Point startPoint = new(), Point endPoint = new(), bool isVR = false)
        {
            if (image == null)
                return;

            CloseWindow();
            ResetDefaultState();

            IsVR = isVR;
            CapturedImage = SetBitmapDimension(image, IsVR);
            CapturedImageEditable = CapturedImage;

            if (showOption)
            {
                ConfigDispatcher = new DispatcherFrame();
                DispatcherFrame cache = ConfigDispatcher;

                SetWindowSize();
                SetWindowPosition(new Point
                {
                    X = endPoint.X - (this.ActualWidth / 2),
                    Y = endPoint.Y - (this.ActualHeight * 2),
                });
                OpenWindow(true);

                Dispatcher.PushFrame(ConfigDispatcher);

                if (cache != ConfigDispatcher)
                    return;
            }

            OpenWindow();
            ChangeCaptureImage(CapturedImageEditable);

            if (App.setting.LookupOnImage)
            {
                SetWindowSize();

                Point gotoPoint = endPoint;

                if (endPoint.X > startPoint.X)
                    gotoPoint.X -= endPoint.X - startPoint.X;
                if (endPoint.Y > startPoint.Y)
                    gotoPoint.Y -= endPoint.Y - startPoint.Y;

                SetWindowPosition(gotoPoint);
            }
            else
            {
                SetWindowSize();
                SetWindowPosition();
            }

            ProcessImage(CapturedImageEditable);
        }
        #endregion

        #region Image & OCR Processing
        private void ProcessImage(Bitmap image)
        {
            UpdateOverlayScale();
            ProcessImageOverlay.Visibility = Visibility.Visible;

            Task.Run(async () =>
            {
                try
                {
                    using var tesseractPage = await TesseractHelper.RunOcrAsync(TesseractEngine, image).ConfigureAwait(false);
                    var pageText = tesseractPage.Text;
                    var captureWords = TesseractHelper.CaptureWordsySimplify(tesseractPage);
                    var altoEntries = App.setting.LookupOnImage ? TesseractHelper.AltoTextProcess(tesseractPage) : null;

                    int historyId = 0;
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        historyId = await HistoryLogger.Add(pageText, captureWords, string.Empty, App.setting.SourceLanguage, App.setting.TargetLanguage).ConfigureAwait(false);
                    }

                    double windowWidth = 800;
                    await Dispatcher.InvokeAsync(() => windowWidth = this.Width);

                    var cardWords = (!App.setting.LookupOnImage && !string.IsNullOrWhiteSpace(pageText))
                        ? Convertor.ConvertCaptureWordsEntry(captureWords, App.setting.SourceLanguage, App.setting.TargetLanguage, windowWidth)
                        : null;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ProcessImageCancelToken.IsCancellationRequested) return;
                        TesseractPageText = pageText;
                        LastHistoryID = historyId;

                        if (string.IsNullOrWhiteSpace(TesseractPageText))
                        {
                            originalCard.Visibility = Visibility.Collapsed;
                            translatedCard.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            ocrText.Text = TesseractPageText;

                            if (App.setting.LookupOnImage)
                                AltoText.ItemsSource = altoEntries;
                            else
                            {
                                originalWords.ItemsSource = cardWords;
                                originalWordsLoading.Visibility = Visibility.Collapsed;
                                originalCard.Visibility = Visibility.Visible;
                                translatedCard.Visibility = Visibility.Visible;
                                translationMessage.SetParagraph(TesseractPageText, App.setting.SourceLanguage, App.setting.TargetLanguage);
                            }

                            if (!App.setting.LookupOnImage)
                            {
                                SetWindowSize();
                                SetWindowPosition();
                            }
                        }
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    SnackbarHost.Show("CaptureWindow", $"OCR Error: {ex.Message}", type: SnackbarType.Error);
                }
                finally
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ProcessImageOverlay.Visibility = Visibility.Collapsed;

                        if (translationExpander.IsExpanded)
                            TranlsateImageExpander();
                    });
                }
            }, ProcessImageCancelToken.Token);
        }

        private static Bitmap SetBitmapDimension(Bitmap bmp, bool limit = false)
        {
            if (limit) // Fixed dimension
            {
                int canvasWidth = 1080;
                int canvasHeight = 720;

                double ratioX = (double)canvasWidth / bmp.Width;
                double ratioY = (double)canvasHeight / bmp.Height;
                double ratio = Math.Min(ratioX, ratioY);

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
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    g.DrawImage(bmp, posX, posY, newWidth, newHeight);
                }

                return resizedBmp;
            }
            else
            {
                double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
                double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

                int maxW = (int)Math.Max(100, screenWidth - 50);
                int maxH = (int)Math.Max(100, screenHeight - 105);

                // Screen boundary hit
                if (bmp.Width > maxW || bmp.Height > maxH)
                {
                    double ratioX = (double)maxW / bmp.Width;
                    double ratioY = (double)maxH / bmp.Height;
                    double ratio = Math.Min(ratioX, ratioY);

                    int newWidth = (int)(bmp.Width * ratio);
                    int newHeight = (int)(bmp.Height * ratio);

                    if (newWidth < 1) newWidth = 1;
                    if (newHeight < 1) newHeight = 1;

                    Bitmap resizedBmp = new Bitmap(newWidth, newHeight);

                    using (Graphics g = Graphics.FromImage(resizedBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                        g.DrawImage(bmp, 0, 0, newWidth, newHeight);
                    }

                    return resizedBmp;
                }

                return bmp;
            }
        }

        private async Task TranlsateImageExpander()
        {
            double expandeHeight = captureCard.ActualHeight - expanderHeader.ActualHeight - expanderContent.Padding.Top - 5;
            expanderContent.Height = expandeHeight;

            double expandeWidth = captureCard.ActualWidth;
            expanderContent.Width = expandeWidth;

            double translationHeight = expandeHeight - expanderContent.Padding.Top - expanderContent.Padding.Bottom;
            translationImage.Height = translationHeight;

            await translationImage.Translate(isWord: false, TesseractPageText, App.setting.SourceLanguage, App.setting.TargetLanguage, TranslatesCancelToken);
            translationMessage.Translated = translationImage.Translated;
            HistoryLogger.Update(LastHistoryID, translationImage.Translated);
        }

        private void ChangeCaptureImage(Bitmap bmp)
        {
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
        #endregion

        #region Window Management & Layout
        public void OpenWindow(bool isConfig = false)
        {
            if (isConfig)
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
                ProcessImageCancelToken = new CancellationTokenSource();
                TranslatesCancelToken = new CancellationTokenSource();

                configMenu.Visibility = Visibility.Collapsed;

                if (App.setting.LookupOnImage)
                {
                    captureCard.Visibility = Visibility.Visible;
                    captureCardButton.Visibility = Visibility.Visible;
                    translationExpander.Visibility = Visibility.Visible;
                    originalCard.Visibility = Visibility.Collapsed;
                    translatedCard.Visibility = Visibility.Collapsed;
                }
                else
                {
                    captureCard.Visibility = App.setting.ShowImage ? Visibility.Visible : Visibility.Collapsed;
                    captureCardButton.Visibility = Visibility.Collapsed;
                    translationExpander.Visibility = Visibility.Collapsed;
                    originalCard.Visibility = Visibility.Visible;
                    translatedCard.Visibility = Visibility.Visible;
                }

                this.Topmost = App.setting.Topmost || IsVR;
            }

            TopmostButton.Visibility = IsVR ? Visibility.Collapsed : Visibility.Visible;

            this.Show();
            this.Activate();
        }

        public void CloseWindow()
        {
            ConfigDispatcher?.Continue = false;
            ConfigDispatcher = null;

            flayOut.IsOpen = false;
            TextToSpeech.StopTTS();

            ProcessImageCancelToken?.Cancel();
            TranslatesCancelToken?.Cancel();

            this.Hide();
        }

        private void ResetDefaultState()
        {
            ocrCard.Visibility = Visibility.Collapsed;
            configMenu.Visibility = Visibility.Collapsed;
            captureCard.Visibility = Visibility.Collapsed;
            captureCardButton.Visibility = Visibility.Collapsed;
            translationExpander.Visibility = Visibility.Collapsed;
            originalCard.Visibility = Visibility.Collapsed;
            translatedCard.Visibility = Visibility.Collapsed;

            originalWordsLoading.Visibility = Visibility.Visible;
            ProcessImageOverlay.Visibility = Visibility.Collapsed;
            Contol_Undo.Visibility = Visibility.Collapsed;
            Control_Confirm_Overlay.Visibility = Visibility.Collapsed;

            AltoText.ItemsSource = null;
            originalWords.ItemsSource = null;
            ocrText.Text = string.Empty;

            captureImage.Source = null;
            captureImage.Width = 0;
            captureImage.Height = 0;

            originalScrollView.ScrollToTop();

            Grid.SetRow(flayOut, App.setting.LookupOnImage ? 1 : 3);

            CloseTranslatedExpanded();

            flayOut.ClearCache();
            translationImage.Clear();
            translationMessage.Clear();
            translatedCache.Clear();

            editRotate = 0;
            editZoom = 1.0;

            this.Left = -10000;
            this.UpdateLayout();
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

            this.MaxWidth = screenWidth;
            this.MaxHeight = screenHeight;
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

        private void App_Deactivated(object sender, EventArgs e)
        {
            if (App.setting.CloseLostFocus && !App.setting.Topmost && !IsVR)
                CloseWindow();
        }

        public void SelectConfigLanguage()
        {
            foreach (ComboBoxItem item in sourceLanguageConfig.Items)
            {
                if (int.Parse(item.Tag.ToString() ?? "0") == App.setting.SourceLanguage)
                {
                    sourceLanguageConfig.SelectedItem = item;
                    break;
                }
            }

            targetLanguageConfig.SelectedIndex = App.setting.TargetLanguage;
        }

        private void CloseTranslatedExpanded()
        {
            if (translationExpander.IsExpanded)
                translationExpander.IsExpanded = false;
        }
        #endregion

        #region UI Event Handlers
        private void CopyImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (captureImage.Source is BitmapSource bitmapSource)
            {
                Clipboard.SetImage(bitmapSource);
                SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
            }
            CloseTranslatedExpanded();
        }

        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (CapturedImageEditable == null && CapturedImage == null && captureImage.Source is not BitmapSource) return;

            Microsoft.Win32.SaveFileDialog saveFileDialog = new()
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap Image (*.bmp)|*.bmp",
                DefaultExt = "png",
                FileName = $"ScreenLookup_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    if (CapturedImageEditable != null || CapturedImage != null)
                    {
                        Bitmap bmpToSave = CapturedImageEditable ?? CapturedImage!;
                        System.Drawing.Imaging.ImageFormat format = Path.GetExtension(saveFileDialog.FileName).ToLower() switch
                        {
                            ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                            ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                            _ => System.Drawing.Imaging.ImageFormat.Png
                        };
                        bmpToSave.Save(saveFileDialog.FileName, format);
                    }
                    else if (captureImage.Source is BitmapSource bitmapSource)
                    {
                        using var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create);
                        BitmapEncoder encoder = Path.GetExtension(saveFileDialog.FileName).ToLower() switch
                        {
                            ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
                            ".bmp" => new BmpBitmapEncoder(),
                            _ => new PngBitmapEncoder()
                        };
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(fileStream);
                    }

                    SnackbarHost.Show("Saved", "Image saved successfully.", type: SnackbarType.Success);
                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("Error", $"Failed to save image:\n{ex.Message}", type: SnackbarType.Error);
                }
            }
            CloseTranslatedExpanded();
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            App.ToggleTopmost(!App.setting.Topmost);
            CloseTranslatedExpanded();
        }

        private void Button_Word(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            string word = button.ToolTip?.ToString() ?? string.Empty;
            int sourceLang = int.Parse(button.Tag?.ToString() ?? "0");

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.Show(word, string.Empty, sourceLang, App.setting.TargetLanguage);
        }

        private void Button_Message(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            string word = button.ToolTip?.ToString() ?? string.Empty;
            string message = button.Uid?.ToString() ?? string.Empty;
            int sourceLang = int.Parse(button.Tag?.ToString() ?? "0");

            int occurrenceIndex = -1;
            if (button.DataContext is CaptureAltoEntry entry)
                occurrenceIndex = entry.WordOccurrenceIndex;

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.Show(word, message, sourceLang, App.setting.TargetLanguage, occurrenceIndex);
            CloseTranslatedExpanded();
        }

        private void Button_OriginalTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(ocrText.Text, App.setting.SourceLanguage);
        }

        private void Button_Copy(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            Clipboard.SetText(button.Tag?.ToString() ?? string.Empty);
            SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
        }

        private void captureWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseTranslatedExpanded();
        }
        #endregion

        #region Image Editing Toolbar Handlers
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (CapturedImage == null) return;

            isImageEditing = false;
            AltoText.ItemsSource = null;
            Contol_Undo.Visibility = Visibility.Hidden;
            Control_Confirm_Overlay.Visibility = Visibility.Visible;

            CapturedImageEditable = CapturedImage;
            editRotate = 0;
            editZoom = 1.0;

            ApplyCaptureEdit();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (CapturedImageEditable == null) return;

            isImageEditing = false;
            AltoText.ItemsSource = null;

            if (CapturedImageEditable == CapturedImage)
                Contol_Undo.Visibility = Visibility.Collapsed;

            Control_Confirm_Overlay.Visibility = Visibility.Collapsed;
            ApplyCaptureEdit();
            ProcessImage(CapturedImageEditable);
        }

        private void ApplyCaptureEdit()
        {
            if (CapturedImage == null) return;

            rotationAngleText?.Text = $"{editRotate:0.#}°";
            scalingText?.Text = $"{Math.Round(editZoom * 100)}%";

            CapturedImageEditable = CapturedImage;
            CapturedImageEditable = Convertor.BitmapRescale(CapturedImageEditable, editZoom);
            CapturedImageEditable = Convertor.BitmapRotate(CapturedImageEditable, editRotate);
            CapturedImageEditable = SetBitmapDimension(CapturedImageEditable);

            ChangeCaptureImage(CapturedImageEditable);
            if (!isImageEditing)
            {
                SetWindowSize();
                SetWindowPosition(new Point(this.Left, this.Top));
            }
        }
        #endregion

        #region Vertical Image Rotation Drag Control
        private void RotationControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                isDraggingRotation = true;
                rotationDragStartPoint = e.GetPosition(this);
                rotationDragStartAngle = editRotate;
                element.CaptureMouse();

                AltoText.ItemsSource = null;
                Contol_Undo.Visibility = Visibility.Visible;
                Control_Confirm_Overlay.Visibility = Visibility.Visible;
            }
        }

        private void RotationControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingRotation)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaY = rotationDragStartPoint.Y - currentPoint.Y;
                double sensitivity = 0.2;
                double newAngle = rotationDragStartAngle + (deltaY * sensitivity);

                // Normalize angle to -180 .. +180
                newAngle %= 360;
                if (newAngle > 180) newAngle -= 360;
                else if (newAngle < -180) newAngle += 360;

                isImageEditing = true;
                editRotate = (float)Math.Round(newAngle, 1);

                ApplyCaptureEdit();
            }
        }

        private void RotationControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingRotation && sender is UIElement element)
            {
                isDraggingRotation = false;
                element.ReleaseMouseCapture();
                ApplyCaptureEdit();
            }
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            if (e is MouseButtonEventArgs mbe)
                mbe.Handled = true;

            AltoText.ItemsSource = null;
            Contol_Undo.Visibility = Visibility.Visible;
            Control_Confirm_Overlay.Visibility = Visibility.Visible;

            editRotate -= 1;
            ApplyCaptureEdit();
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            if (e is MouseButtonEventArgs mbe)
                mbe.Handled = true;

            AltoText.ItemsSource = null;
            Contol_Undo.Visibility = Visibility.Visible;
            Control_Confirm_Overlay.Visibility = Visibility.Visible;

            editRotate += 1;
            ApplyCaptureEdit();
        }

        #endregion

        #region Horizontal Image Scaling Drag Control
        private void ScalingControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                isDraggingScaling = true;
                scalingDragStartPoint = e.GetPosition(this);
                scalingDragStartZoom = editZoom;
                element.CaptureMouse();

                AltoText.ItemsSource = null;
                Contol_Undo.Visibility = Visibility.Visible;
                Control_Confirm_Overlay.Visibility = Visibility.Visible;
            }
        }

        private void ScalingControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingScaling)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - scalingDragStartPoint.X;

                double sensitivity = 0.002;
                double newZoom = scalingDragStartZoom + (deltaX * sensitivity);

                isImageEditing = true;
                newZoom = Math.Clamp(newZoom, 0.1, 5.0);
                editZoom = Math.Round(newZoom, 2);

                ApplyCaptureEdit();
            }
        }

        private void ScalingControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingScaling && sender is UIElement element)
            {
                isDraggingScaling = false;
                element.ReleaseMouseCapture();
                ApplyCaptureEdit();
            }
        }

        private void ScaleDown_Click(object sender, RoutedEventArgs e)
        {
            if (e is MouseButtonEventArgs mbe)
                mbe.Handled = true;

            AltoText.ItemsSource = null;
            Contol_Undo.Visibility = Visibility.Visible;
            Control_Confirm_Overlay.Visibility = Visibility.Visible;

            editZoom = Math.Max(0.1, Math.Round(editZoom - 0.1, 2));
            ApplyCaptureEdit();
        }

        private void ScaleUp_Click(object sender, RoutedEventArgs e)
        {
            if (e is MouseButtonEventArgs mbe)
                mbe.Handled = true;

            AltoText.ItemsSource = null;
            Contol_Undo.Visibility = Visibility.Visible;
            Control_Confirm_Overlay.Visibility = Visibility.Visible;

            editZoom = Math.Min(5.0, Math.Round(editZoom + 0.1, 2));
            ApplyCaptureEdit();
        }
        #endregion

        #region Quick Config Handlers
        private void SourceLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                    App.setting.SourceLanguage = int.Parse(selectedItem.Tag.ToString() ?? "0");
            }
        }

        private void TargetLanguageConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                    App.setting.TargetLanguage = int.Parse(selectedItem.Tag.ToString() ?? "0");
            }
        }

        private void ConfigSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigDispatcher != null)
                ConfigDispatcher.Continue = false;
        }

        private void ConfigSwitch_Toggle(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch switchs && switchs.Name == "hunSpell")
            {
                if (!HunspellHelper.IsInstalled(App.setting.SourceLanguage))
                    SnackbarHost.Show("Hunspell", $"You have to download Hunspell \"{LanguageList.GetDisplayNameFromID(App.setting.SourceLanguage, true)}\"", SnackbarType.Error);
            }
        }
        #endregion
    }
}
