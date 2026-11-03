using PaddleOCRSharp;
using ScreenLookup.src.models;
using System.Drawing;

namespace ScreenLookup.src.utils
{
    public static class OcrHelper
    {
        private static readonly SemaphoreSlim OcrLock = new(1, 1);

        public static dynamic? Engine;
        public static string Paragraph = string.Empty;

        public enum OcrEngine
        {
            TesseraceOCR,
            PaddleOCR
        }

        public static bool CreateOcrEngine()
        {
            Engine?.Dispose();
            Engine = null;
            Paragraph = string.Empty;

            switch (App.setting.OcrEngineMode)
            {
                case OcrEngine.TesseraceOCR:
                    if (TesseractHelper.IsInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
                    {
                        try
                        {
                            Engine = new TesseractOCR.Engine(
                                TesseractHelper.GetTessdataPath(App.setting.SourceLanguageAccuracy),
                                LanguageList.GetTesseractTagFromID(App.setting.SourceLanguage),
                                TesseractOCR.Enums.EngineMode.Default);
                        }
                        catch (Exception ex)
                        {
                            SnackbarHost.Show($"Failed to create Tesseract engine: {ex.Message}", type: SnackbarType.Error);
                            return false;
                        }
                    }

                    break;
                case OcrEngine.PaddleOCR:
                    if (PaddleHelper.IsModelInstalled(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage))
                    {
                        try
                        {
                            var (cfg, parameter) = PaddleHelper.GetModelConfig(App.setting.SourceLanguageAccuracy, App.setting.SourceLanguage);
                            Engine = new PaddleOCREngine(cfg, parameter);
                        }
                        catch (Exception ex)
                        {
                            SnackbarHost.Show($"Failed to create Paddle engine: {ex.Message}", type: SnackbarType.Error);
                            return false;
                        }
                    }

                    break;
            }

            return true;
        }

        public static List<CaptureAltoEntry> AltoTextProcess(dynamic ocrResult, Bitmap image)
        {
            return App.setting.OcrEngineMode switch
            {
                OcrEngine.TesseraceOCR => (List<CaptureAltoEntry>)TesseractHelper.AltoTextProcess(ocrResult),
                OcrEngine.PaddleOCR => (List<CaptureAltoEntry>)PaddleHelper.AltoTextProcess(ocrResult, image),
                _ => throw new NotImplementedException(),
            };
        }

        public static List<ImageTranslateEntry> TranslateImageProcess(dynamic ocrResult, Bitmap image)
        {
            return App.setting.OcrEngineMode switch
            {
                OcrEngine.TesseraceOCR => TesseractHelper.TranslateImageProcess(ocrResult, image),
                OcrEngine.PaddleOCR => PaddleHelper.TranslateImageProcess(ocrResult, image),
                _ => throw new NotImplementedException(),
            };
        }

        public static (System.Windows.Media.Brush Background, System.Windows.Media.Brush Foreground)? GetZoneAverageColors(Bitmap? image, int x, int y, int w, int h)
        {
            if (image == null || w <= 0 || h <= 0)
                return null;

            try
            {
                int cropX = Math.Clamp(x, 0, image.Width - 1);
                int cropY = Math.Clamp(y, 0, image.Height - 1);
                int cropW = Math.Clamp(w, 1, image.Width - cropX);
                int cropH = Math.Clamp(h, 1, image.Height - cropY);

                long totalR = 0, totalG = 0, totalB = 0;
                int bgCount = 0;
                long totalFgR = 0, totalFgG = 0, totalFgB = 0;
                int fgCount = 0;

                lock (image)
                {
                    using Bitmap tempBmp = new(cropW, cropH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(tempBmp))
                    {
                        g.DrawImage(image, new Rectangle(0, 0, cropW, cropH), new Rectangle(cropX, cropY, cropW, cropH), GraphicsUnit.Pixel);
                    }

                    System.Drawing.Imaging.BitmapData data = tempBmp.LockBits(
                        new Rectangle(0, 0, cropW, cropH),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    try
                    {
                        int stride = data.Stride;
                        byte[] pixels = new byte[stride * cropH];
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                        // Build luminance histogram (16 bins)
                        int[] histogram = new int[16];
                        for (int py = 0; py < cropH; py++)
                        {
                            int rowOffset = py * stride;
                            for (int px = 0; px < cropW; px++)
                            {
                                int index = rowOffset + (px * 4);
                                if (index + 2 < pixels.Length)
                                {
                                    int b = pixels[index];
                                    int gVal = pixels[index + 1];
                                    int r = pixels[index + 2];
                                    int lum = (int)(0.299 * r + 0.587 * gVal + 0.114 * b);
                                    int bin = Math.Min(15, lum / 16);
                                    histogram[bin]++;
                                }
                            }
                        }

                        // Identify the dominant background luminance bin (peak pixel cluster)
                        int peakBin = 0;
                        int maxCount = 0;
                        for (int i = 0; i < 16; i++)
                        {
                            if (histogram[i] > maxCount)
                            {
                                maxCount = histogram[i];
                                peakBin = i;
                            }
                        }

                        // Filter and average background pixels (within peakBin +- 2) and foreground text pixels (outside)
                        int minLum = Math.Max(0, (peakBin - 2) * 16);
                        int maxLum = Math.Min(255, (peakBin + 3) * 16 - 1);

                        for (int py = 0; py < cropH; py++)
                        {
                            int rowOffset = py * stride;
                            for (int px = 0; px < cropW; px++)
                            {
                                int index = rowOffset + (px * 4);
                                if (index + 2 < pixels.Length)
                                {
                                    int b = pixels[index];
                                    int gVal = pixels[index + 1];
                                    int r = pixels[index + 2];
                                    int lum = (int)(0.299 * r + 0.587 * gVal + 0.114 * b);

                                    if (lum >= minLum && lum <= maxLum)
                                    {
                                        totalB += b;
                                        totalG += gVal;
                                        totalR += r;
                                        bgCount++;
                                    }
                                    else
                                    {
                                        totalFgB += b;
                                        totalFgG += gVal;
                                        totalFgR += r;
                                        fgCount++;
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        tempBmp.UnlockBits(data);
                    }
                }

                if (bgCount == 0) return null;

                byte avgR = (byte)(totalR / bgCount);
                byte avgG = (byte)(totalG / bgCount);
                byte avgB = (byte)(totalB / bgCount);

                double bgLum = (0.299 * avgR + 0.587 * avgG + 0.114 * avgB);

                System.Windows.Media.Brush bgBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(240, avgR, avgG, avgB));
                System.Windows.Media.Brush fgBrush;

                if (fgCount >= 2)
                {
                    byte fgR = (byte)(totalFgR / fgCount);
                    byte fgG = (byte)(totalFgG / fgCount);
                    byte fgB = (byte)(totalFgB / fgCount);
                    double fgLum = (0.299 * fgR + 0.587 * fgG + 0.114 * fgB);

                    if (Math.Abs(bgLum - fgLum) >= 25.0)
                    {
                        fgBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(fgR, fgG, fgB));
                    }
                    else
                    {
                        fgBrush = bgLum > 128 ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
                    }
                }
                else
                {
                    fgBrush = bgLum > 128 ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
                }

                bgBrush.Freeze();
                fgBrush.Freeze();

                return (bgBrush, fgBrush);
            }
            catch
            {
                return null;
            }
        }

        public static List<CaptureWordsSimplifiedEntry> CaptureWordsSimplify(dynamic ocrResult)
        {
            return App.setting.OcrEngineMode switch
            {
                OcrEngine.TesseraceOCR => (List<CaptureWordsSimplifiedEntry>)TesseractHelper.CaptureWordsySimplify(ocrResult),
                OcrEngine.PaddleOCR => (List<CaptureWordsSimplifiedEntry>)PaddleHelper.CaptureWordsSimplify(ocrResult),
                _ => [],
            };
        }

        public static string GetOcrText(dynamic ocrResult)
        {
            return App.setting.OcrEngineMode switch
            {
                OcrEngine.TesseraceOCR => (string)(ocrResult.Text ?? string.Empty),
                OcrEngine.PaddleOCR => (string)PaddleHelper.FormatTextLayout(ocrResult),
                _ => string.Empty,
            };
        }

        public static async Task<dynamic?> RunOcrAsync(Bitmap image)
        {
            await OcrLock.WaitAsync().ConfigureAwait(false);
            try
            {
                dynamic? result = await (App.setting.OcrEngineMode switch
                {
                    OcrEngine.TesseraceOCR => TesseractHelper.RunOcrAsync(Engine, image),
                    OcrEngine.PaddleOCR => PaddleHelper.RunOcrAsync(Engine, image),
                    _ => throw new NotImplementedException(),
                }).ConfigureAwait(false);

                Paragraph = result != null ? GetOcrText(result) : string.Empty;
                Paragraph = HunspellHelper.CorrectionParagraph(Paragraph);

                return result;
            }
            finally
            {
                OcrLock.Release();
            }
        }

        public static async Task<bool> InstallModel(SourceAccuracy accID, int langID)
        {
            return App.setting.OcrEngineMode switch
            {
                OcrEngine.TesseraceOCR => await TesseractHelper.InstallModel(accID, langID).ConfigureAwait(false),
                OcrEngine.PaddleOCR => await PaddleHelper.InstallModel(accID, langID).ConfigureAwait(false),
                _ => false,
            };
        }

        public static void Dispose()
        {
            Engine?.Dispose();
            Engine = null;
        }
    }

    public enum SourceAccuracy
    {
        Fast,
        Normal,
        Slow
    }

    public static class SourceAccuracyExtensions
    {
        public static string GetDisplayName(this SourceAccuracy accuracy) => accuracy switch
        {
            SourceAccuracy.Fast => "Fast (Bad)",
            SourceAccuracy.Normal => "Normal",
            SourceAccuracy.Slow => "Slow (Accurate)",
            _ => accuracy.ToString()
        };
    }

}
