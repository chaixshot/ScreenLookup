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
                    if (PaddleHelper.IsInstalled(App.setting.SourceLanguageAccuracy))
                    {
                        try
                        {
                            var (cfg, parameter) = PaddleHelper.GetModelConfig(App.setting.SourceLanguageAccuracy);
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

                return result;
            }
            finally
            {
                OcrLock.Release();
            }
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
