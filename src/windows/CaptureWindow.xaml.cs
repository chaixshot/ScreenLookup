using HunspellSharp;
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
                {
                    HideWindow();
                }
            };
        }

        public void LoadInstalledLanguage()
        {
            List<ComboBoxItem> sourceItems = [];
            List<string> targetItems = [];

            for (int langID = 0; langID < LanguageList.LanguageTesseract.Length - 1; langID++)
            {
                string languageTesseract = LanguageList.LanguageTesseract[langID];
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

        private void HideWindow()
        {
            IsCapturing = false;
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
            if (image == null)
            {
                IsCapturing = false;
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
                    TesseractOCR.Page tesseract = await Task.Run(() => GetTesseractPageFromBitmap(image));

                    if (IsCapturing)
                    {
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
                            List<CaptureWordsEntrySimplify> captureWords = await Task.Run(() => TesseractCaptureWordsySimplify(tesseract));
                            if (IsCapturing)
                            {
                                originalWords.ItemsSource = Convertor.ConvertCaptureWordsEntry(captureWords, App.setting.SourceLanguage, App.setting.TargetLanguage, this.Width);
                                originalWordsLoading.Visibility = Visibility.Collapsed;

                                // Translate card
                                string translateResult = await LanguageList.TranslatedText(tesseract.Text, App.setting.TargetLanguage);
                                translatedText.Text = translateResult;
                                translatedTextLoading.Visibility = Visibility.Collapsed;

                                if (IsCapturing)
                                {
                                    await AddToHistory(ocrText.Text, captureWords, translateResult);

                                    CenterWindowOnScreen(image.Width, image.Height);
                                }
                            }
                        }

                        IsCapturing = false;
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

            originalTTS.Width = buttonWidth;
            originalTTS.Height = buttonWidth;

            translatedTSS.Width = buttonWidth;
            translatedTSS.Height = buttonWidth;

            captureCard.Visibility = App.setting.ShowImage ? Visibility.Visible : Visibility.Collapsed;

            translatedTextLoading.Visibility = Visibility.Visible;
            originalWordsLoading.Visibility = Visibility.Visible;

            originalCard.Visibility = Visibility.Visible;
            translatedCard.Visibility = Visibility.Visible;

            originalWords.ItemsSource = null;
            translatedText.Text = "";

            originalScrollView.ScrollToTop();
            translatedScrollViewer.ScrollToTop();

            this.Topmost = App.setting.Topmost;
        }

        private async void Button_Word(object sender, RoutedEventArgs e)
        {

            flayOut.flayOut.IsOpen = false;

            Button? button = sender as Button;
            string word = button.ToolTip.ToString();
            int sourceLanguage = Int32.Parse(button.Tag.ToString());

            if (string.IsNullOrWhiteSpace(word))
                return;

            flayOut.originalWord.Text = word;
            flayOut.originalWord.Tag = sourceLanguage;
            flayOut.translatedWord.Tag = App.setting.TargetLanguage;

            flayOut.flayOut.IsOpen = true;
        }

        private void CenterWindowOnScreen(double imgWidth, double imgHeight)
        {
            double screenWidth = System.Windows.SystemParameters.WorkArea.Width;
            double screenHeight = System.Windows.SystemParameters.WorkArea.Height;

            captureImage.Width = Math.Min(imgWidth, screenWidth);
            captureImage.Height = Math.Min(imgHeight, screenHeight / 2);

            this.MaxWidth = screenWidth - 50;
            this.MaxHeight = screenHeight - 50;
            this.Width = Math.Min(this.MaxWidth, captureImage.Width + (App.setting.FontSizeS * 10));

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

        private async Task<List<CaptureWordsEntrySimplify>> TesseractCaptureWordsySimplify(TesseractOCR.Page page)
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
                                if (!IsCapturing)
                                    goto skip;

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

        skip:

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
            TextToSpeech.StartTTS(ocrText.Text, App.setting.SourceLanguage, "capture");
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            TextToSpeech.StartTTS(translatedText.Text, App.setting.TargetLanguage, "capture");
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

        private void Submit_Click(object sender, RoutedEventArgs e)
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
