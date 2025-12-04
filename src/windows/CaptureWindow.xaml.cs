using GTranslate.Translators;
using NAudio.Wave;
using ScreenGrab;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Layout;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace ScreenLookup.src.windows
{
    /// <summary>
    /// Interaction logic for Capture.xaml
    /// </summary>
    public partial class CaptureWindow : Window
    {
        private CancellationTokenSource CTS = new CancellationTokenSource();
        private class TodoItem
        {
            public string Word { get; set; }
        }

        public CaptureWindow()
        {
            InitializeComponent();
            StartCaptureAndTranslate();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    this.Hide();
            };
        }

        private async void StartCaptureAndTranslate()
        {
            // Screenshot   
            Bitmap image = ScreenGrabber.CaptureDialog(false);

            if (image == null)
            {
                this.Hide();
            }
            else
            {
                BitmapSource writeBmp = GetImageSourceFromBitmap(image);

                // Window size
                captureImage.Source = writeBmp;
                this.Width = writeBmp.Width + 50;

                // Image
                MemoryStream ms = new MemoryStream();
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] fileBytes = ms.ToArray();

                // TesseractOCR
                var engine = new Engine(@"C:\Program Files\Tesseract-OCR\tessdata", "eng", EngineMode.Default);
                var img = TesseractOCR.Pix.Image.LoadFromMemory(fileBytes);
                var page = engine.Process(img);

                if (page.Text != "")
                {
                    // Text
                    // Create an instance of the Google Translator
                    ocrText.Text = page.Text;

                    var translator = new GoogleTranslator2();
                    var translateResult = await translator.TranslateAsync(page.Text, "th");
                    translatedText.Text = translateResult.Translation;

                    ////////////////////////////
                    ////////////////////////////
                    ////////////////////////////

                    List<TodoItem> items = new List<TodoItem>();
                    foreach (var block in page.Layout)
                    {
                        foreach (var paragraph in block.Paragraphs)
                        {
                            foreach (var textLine in paragraph.TextLines)
                            {
                                foreach (var word in textLine.Words)
                                {
                                    items.Add(new TodoItem() { Word = word.Text });
                                }
                            }
                        }
                    }
                    ocrWords.ItemsSource = items;
                }
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
            }
        }

        // Paragraph
        private void Button_OriginalTTS(object sender, RoutedEventArgs e)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            PlayTTS(ocrText.Text, "en", CTS);
        }

        private void Button_TranslatedTTS(object sender, RoutedEventArgs e)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            PlayTTS(translatedText.Text, "th", CTS);
        }

        // Word
        private async void Button_Word(object sender, RoutedEventArgs e)
        {
            Button? word = sender as Button;

            string originalWord = word.Content.ToString();
            definition_original.Text = originalWord;

            var translator = new GoogleTranslator2();
            var translateResult = await translator.TranslateAsync(originalWord, "th");
            definition_translated.Text = translateResult.Translation;
        }

        private async void Button_WordOriginalTTS(object sender, RoutedEventArgs e)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            PlayTTS(definition_original.Text, "en", CTS);
        }

        private async void Button_WordTranslatedTTS(object sender, RoutedEventArgs e)
        {
            CTS.Cancel();
            CTS = new CancellationTokenSource();
            PlayTTS(definition_translated.Text, "th", CTS);
        }

        // Utility
        void App_Deactivated(object sender, EventArgs e)
        {
            CTS.Cancel();
            this.Hide();
        }
    }
}
