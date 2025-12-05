using GTranslate.Translators;
using HotkeyUtility;
using NAudio.Wave;
using ScreenGrab;
using ScreenLookup.src.models;
using ScreenLookup.src.pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Layout;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.windows
{
    /// <summary>
    /// Interaction logic for Capture.xaml
    /// </summary>
    public partial class CaptureWindow : FluentWindow
    {
        private CancellationTokenSource CTS = new CancellationTokenSource();
        private class WordItem
        {
            public string Word { get; set; }
            public string Width { get; set; }
            public string Height { get; set; }
            public string Border { get; set; }
        }

        public CaptureWindow()
        {
            InitializeComponent();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    this.Close();
            };
            Loaded += (s, e) =>
            {
                originalText.Text = "";
                translatedText.Text = "";
                definition.Visibility = Visibility.Collapsed;

                StartCaptureAndTranslate();
            };
        }

        private async void StartCaptureAndTranslate()
        {
            if (!Setting.IsLangugeInstalled(Setting.SourceLanguge))
            {
                Notification.Show($"Install {LangugeList.CultureDisplayName(LangugeList.GetTesseractTagFromID(Setting.SourceLanguge))} in the setting", 1000);
                this.Close();
                return;
            }

            // Screenshot   
            Bitmap image = ScreenGrabber.CaptureDialog(false);
            if (image == null)
            {
                Notification.Show("No image has been captured", 1000);
                this.Close();
                return;
            }

            BitmapSource writeBmp = GetImageSourceFromBitmap(image);

            // Window size
            captureImage.Source = writeBmp;
            this.Width = writeBmp.Width + 50;
            this.MinWidth = this.Width;
            this.MaxWidth = this.Width;

            // Image
            MemoryStream ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] fileBytes = ms.ToArray();

            // TesseractOCR
            var engine = new Engine($"{AppDomain.CurrentDomain.BaseDirectory}\\tessdata", LangugeList.GetTesseractTagFromID(Setting.SourceLanguge), EngineMode.Default);
            var img = TesseractOCR.Pix.Image.LoadFromMemory(fileBytes);
            var page = engine.Process(img);

            if (page.Text == "")
            {
                originalWords.Visibility = Visibility.Collapsed;
                translatedCard.Visibility = Visibility.Collapsed;
            }
            else
            {
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
                                items.Add(new WordItem() { Word = word.Text, Border = "1" });
                            }
                            items.Add(new WordItem() { Word = "", Width = this.Width.ToString(), Height = "0" });
                        }
                        items.Add(new WordItem() { Word = "", Width = this.Width.ToString() });
                    }
                    items.Add(new WordItem() { Word = "", Width = this.Width.ToString() });
                }
                ocrWords.ItemsSource = items;

                // Translated text
                var translator = new GoogleTranslator2();
                var translateResult = await translator.TranslateAsync(page.Text, LangugeList.GetLanguageShortageFromID(Setting.TargetLanguge));
                translatedText.Text = translateResult.Translation;
            }
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

        private static async void PlayTTS(string Text, string Languge, CancellationTokenSource token)
        {
            var translator = new GoogleTranslator2();
            Stream stream = await translator.TextToSpeechAsync(Text, Languge, false);

            Stream ms = new MemoryStream();
            byte[] buffer = new byte[32768];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            ms.Position = 0;
            using (WaveStream blockAlignedStream =
                new BlockAlignReductionStream(
                    WaveFormatConversionStream.CreatePcmStream(
                        new Mp3FileReader(ms))))
            {
                WaveOut waveOut = new WaveOut(WaveCallbackInfo.FunctionCallback());
                waveOut.Init(blockAlignedStream);
                waveOut.Play();
                while (waveOut.PlaybackState == PlaybackState.Playing && !token.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }
                waveOut.Dispose();
            }
        }

        private void StartTTS(string Text, string Languge)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            PlayTTS(Text, Languge, CTS);
        }

        // Paragraph
        private void Button_OriginalTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(originalText.Text, LangugeList.GetLanguageShortageFromID(Setting.SourceLanguge));
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(translatedText.Text, LangugeList.GetLanguageShortageFromID(Setting.TargetLanguge));
        }

        // Word
        private async void Button_Word(object sender, RoutedEventArgs e)
        {
            Button? word = sender as Button;

            definition.Visibility = Visibility.Visible;

            string originalWord = word.Content.ToString();
            definition_original.Text = originalWord;
            definition_translated.Text = "...";

            StartTTS(definition_original.Text, LangugeList.GetLanguageShortageFromID(Setting.SourceLanguge));

            var translator = new GoogleTranslator2();
            var translateResult = await translator.TranslateAsync(originalWord, LangugeList.GetLanguageShortageFromID(Setting.TargetLanguge));
            definition_translated.Text = translateResult.Translation;
        }

        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(definition_original.Text, LangugeList.GetLanguageShortageFromID(Setting.SourceLanguge));
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            StartTTS(definition_translated.Text, LangugeList.GetLanguageShortageFromID(Setting.TargetLanguge));
        }

        // Utility
        void App_Deactivated(object sender, EventArgs e)
        {
            CTS.Cancel();
            this.Close();
        }
    }
}
