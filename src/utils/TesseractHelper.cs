using ScreenLookup.src.models;
using System.Drawing;
using System.Xml;

namespace ScreenLookup.src.utils
{
    public static class TesseractHelper
    {
        public static readonly string[] LangList = [
            "afr", "amh", "ara", "asm", "aze", "aze_cyrl", "bel", "ben", "bod", "bos",
            "bre", "bul", "cat", "ceb", "ces", "chi_sim", "chi_sim_vert", "chi_tra", "chi_tra_vert", "chr",
            "cos", "cym", "dan", "dan_frak", "deu", "deu_frak", "div", "dzo", "ell", "eng",
            "enm", "epo", "equ", "est", "eus", "fao", "fas", "fil", "fin", "fra",
            "frk", "frm", "fry", "gla", "gle", "glg", "grc", "guj", "hat", "heb",
            "hin", "hrv", "hun", "hye", "iku", "ind", "isl", "ita", "ita_old", "jav",
            "jpn", "jpn_vert", "kan", "kat", "kat_old", "kaz", "khm", "kir", "kmr", "kor",
            "kor_vert", "lao", "lat", "lav", "lit", "ltz", "mal", "mar", "mkd", "mlt",
            "mon", "mri", "msa", "mya", "nep", "nld", "nor", "oci", "ori", "osd",
            "pan", "pol", "por", "pus", "que", "ron", "rus", "san", "sin", "slk",
            "slk_frak", "slv", "snd", "spa", "spa_old", "sqi", "srp", "srp_latn", "sun", "swa",
            "swe", "syr", "tam", "tat", "tel", "tgk", "tgl", "tha", "tir", "ton",
            "tur", "uig", "ukr", "urd", "uzb", "uzb_cyrl", "vie", "yid", "yor"
        ];

        public static string GetTesseractModelName(string langTag)
        {
            if (string.IsNullOrEmpty(langTag))
                return "eng";
            else if (langTag.EndsWith("_vert"))
                return LanguageList.ClearLanguageTag(langTag);
            else
                return langTag;
        }

        public static string GetTessdataPath(SourceAccuracy accID)
        {
            return accID switch
            {
                SourceAccuracy.Fast => Path.Combine(App.appDataFolder, "tessdata_fast"),
                SourceAccuracy.Normal => Path.Combine(App.appDataFolder, "tessdata"),
                _ => Path.Combine(App.appDataFolder, "tessdata_best"),
            };
        }

        public static bool IsInstalled(SourceAccuracy accID, int langID)
        {
            if (langID < 0 || langID >= LangList.Length) return false;

            string tessdataPath = GetTessdataPath(accID);
            string rawTag = LangList[langID];
            string modelName = GetTesseractModelName(rawTag);

            return File.Exists(Path.Combine(tessdataPath, $"{modelName}.traineddata"));
        }

        public static async Task<bool> InstallModel(SourceAccuracy accID, int langID)
        {
            if (langID < 0 || langID >= LangList.Length) return false;
            if (IsInstalled(accID, langID)) return true;

            string rawTag = LangList[langID];
            string modelName = GetTesseractModelName(rawTag);
            string pickedLanguageFile = $"{modelName}.traineddata";
            string tesseractFilePath = GetTessdataPath(accID);
            string filePath = Path.Combine(App.tempFolder, pickedLanguageFile);

            try
            {
                string repoName = accID switch
                {
                    SourceAccuracy.Fast => "tessdata_fast",
                    SourceAccuracy.Normal => "tessdata",
                    _ => "tessdata_best",
                };
                string downloadUrl = $"https://raw.githubusercontent.com/tesseract-ocr/{repoName}/main/{pickedLanguageFile}";

                SnackbarHost.Show("Tesseract", $"Downloading {accID.GetDisplayName()} - {LanguageList.GetDisplayNameFromID(langID, true)}...", SnackbarType.Info, timeout: 99999, closeButton: false);

                DownloadHelper fileDownloader = new();
                bool isDownloaded = await fileDownloader.DownloadFileAsync(downloadUrl, filePath);
                if (isDownloaded)
                {
                    await DownloadHelper.MoveFileToFolder(filePath, tesseractFilePath);
                    return File.Exists(Path.Combine(tesseractFilePath, pickedLanguageFile));
                }
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("TesseractHelper", $"Failed to install Tesseract model: {ex.Message}", type: SnackbarType.Error);
            }
            finally
            {
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
                catch { }
            }

            return false;
        }

        public static Task<TesseractOCR.Page> RunOcrAsync(TesseractOCR.Engine engine, Bitmap image)
        {
            using MemoryStream ms = new();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] fileBytes = ms.GetBuffer();

            TesseractOCR.Pix.Image img = TesseractOCR.Pix.Image.LoadFromMemory(fileBytes);
            return Task.FromResult(engine!.Process(img));
        }

        public static List<CaptureWordsSimplifiedEntry> CaptureWordsySimplify(TesseractOCR.Page page)
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
                            string text = HunspellHelper.CorrectionWord(word.Text);
                            if (!string.IsNullOrWhiteSpace(text))
                                items.Add(new CaptureWordsSimplifiedEntry { Word = text, Stop = 0 });
                        }
                        items.Add(new CaptureWordsSimplifiedEntry { Word = string.Empty, Stop = 1 });
                    }
                    items.Add(new CaptureWordsSimplifiedEntry { Word = string.Empty, Stop = 2 });
                }
                items.Add(new CaptureWordsSimplifiedEntry { Word = string.Empty, Stop = 3 });
            }

            return items;
        }

        public static List<CaptureAltoEntry> AltoTextProcess(TesseractOCR.Page page)
        {
            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(page.AltoText);
            List<CaptureAltoEntry> items = [];

            var textBlocks = xmlDoc.GetElementsByTagName("TextBlock");
            foreach (XmlElement textBlock in textBlocks)
            {
                Dictionary<string, int> wordOccurrenceCounter = new(StringComparer.OrdinalIgnoreCase);

                int GetOccurrenceIndex(string w)
                {
                    string clean = AppUtilities.RegexPunctuation().Replace(w, "").Trim().Normalize(System.Text.NormalizationForm.FormC);
                    if (string.IsNullOrEmpty(clean)) return -1;
                    clean = char.ToUpper(clean[0]) + (clean.Length > 1 ? clean[1..].ToLower() : string.Empty);
                    int count = wordOccurrenceCounter.GetValueOrDefault(clean, 0);
                    wordOccurrenceCounter[clean] = count + 1;
                    return count;
                }

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
                        if (!AppUtilities.IsCjk(lastChar) || !AppUtilities.IsCjk(firstChar))
                            fullTextBuilder.Append(' ');
                    }
                    fullTextBuilder.Append(content);
                    prevContent = content;
                }
                string fullTextBlock = fullTextBuilder.ToString();

                var textLines = textBlock.GetElementsByTagName("TextLine");
                if (textLines.Count == 0)
                {
                    foreach (XmlElement data in allStrings)
                    {
                        string word = HunspellHelper.CorrectionWord(data.GetAttribute("CONTENT"));
                        if (string.IsNullOrWhiteSpace(word)) continue;

                        items.Add(new CaptureAltoEntry
                        {
                            Word = word,
                            X = int.TryParse(data.GetAttribute("HPOS"), out int x) ? x : 0,
                            Y = Math.Max(0, (int.TryParse(data.GetAttribute("VPOS"), out int y) ? y : 0) - 2),
                            Width = Math.Max(4, (int.TryParse(data.GetAttribute("WIDTH"), out int w) ? w : 0) + 2),
                            Height = Math.Max(8, (int.TryParse(data.GetAttribute("HEIGHT"), out int h) ? h : 0) + 4),
                            Angle = 0,
                            SourceLanguage = App.setting.SourceLanguage,
                            TargetLanguage = App.setting.TargetLanguage,
                            Uid = fullTextBlock,
                            WordOccurrenceIndex = GetOccurrenceIndex(word),
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

                    // Determine rotation angle of the text line
                    double lineAngle = 0;
                    string rotAttr = textLine.GetAttribute("ROTATION");
                    if (string.IsNullOrEmpty(rotAttr))
                        rotAttr = textBlock.GetAttribute("ROTATION");

                    if (!string.IsNullOrEmpty(rotAttr) && double.TryParse(rotAttr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedRot))
                    {
                        lineAngle = parsedRot;
                    }
                    else if (lineStrings.Count >= 2)
                    {
                        XmlElement firstString = (XmlElement)lineStrings[0];
                        XmlElement lastString = (XmlElement)lineStrings[lineStrings.Count - 1];

                        if (int.TryParse(firstString.GetAttribute("HPOS"), out int x1) &&
                            int.TryParse(firstString.GetAttribute("VPOS"), out int y1) &&
                            int.TryParse(lastString.GetAttribute("HPOS"), out int x2) &&
                            int.TryParse(lastString.GetAttribute("VPOS"), out int y2))
                        {
                            double dx = x2 - x1;
                            double dy = y2 - y1;
                            if (!isVertical && Math.Abs(dx) > 10)
                            {
                                double calculatedAngle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                                if (Math.Abs(calculatedAngle) > 0.5 && Math.Abs(calculatedAngle) < 45.0)
                                {
                                    lineAngle = calculatedAngle;
                                }
                            }
                            else if (isVertical && Math.Abs(dy) > 10)
                            {
                                double calculatedAngle = Math.Atan2(dx, dy) * (180.0 / Math.PI);
                                if (Math.Abs(calculatedAngle) > 0.5 && Math.Abs(calculatedAngle) < 45.0)
                                {
                                    lineAngle = -calculatedAngle;
                                }
                            }
                        }
                    }

                    for (int i = 0; i < lineStrings.Count; i++)
                    {
                        XmlElement data = (XmlElement)lineStrings[i];
                        string word = HunspellHelper.CorrectionWord(data.GetAttribute("CONTENT"));
                        if (string.IsNullOrWhiteSpace(word)) continue;

                        int strX = int.TryParse(data.GetAttribute("HPOS"), out int sx) ? sx : lineX;
                        int strY = int.TryParse(data.GetAttribute("VPOS"), out int sy) ? sy : lineY;
                        int strW = int.TryParse(data.GetAttribute("WIDTH"), out int sw) ? sw : 0;
                        int strH = int.TryParse(data.GetAttribute("HEIGHT"), out int sh) ? sh : 0;

                        double x, y, width, height;

                        if (!isVertical)
                        {
                            y = Math.Max(0, (lineAngle != 0 ? strY : lineY) - 2);
                            height = Math.Max(8, (lineAngle != 0 && strH > 0 ? strH : lineHeight) + 4);
                            x = Math.Max(0, strX);

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
                            x = Math.Max(0, (lineAngle != 0 ? strX : lineX) - 2);
                            width = Math.Max(8, (lineAngle != 0 && strW > 0 ? strW : lineWidth) + 4);
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
                            Angle = lineAngle,
                            SourceLanguage = App.setting.SourceLanguage,
                            TargetLanguage = App.setting.TargetLanguage,
                            Uid = fullTextBlock,
                            WordOccurrenceIndex = GetOccurrenceIndex(word),
                        });
                    }
                }
            }
            return items;
        }

        public static List<ImageTranslateEntry> TranslateImageProcess(TesseractOCR.Page page, Bitmap? image = null)
        {
            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(page.AltoText);
            List<ImageTranslateEntry> items = [];

            var textBlocks = xmlDoc.GetElementsByTagName("TextBlock");
            foreach (XmlElement textBlock in textBlocks)
            {
                int fineTune = 5;

                var textLines = textBlock.GetElementsByTagName("TextLine");
                if (textLines.Count > 0)
                {
                    foreach (XmlElement textLine in textLines)
                    {
                        int lx = int.TryParse(textLine.GetAttribute("HPOS"), out int px) ? px : 0;
                        int ly = int.TryParse(textLine.GetAttribute("VPOS"), out int py) ? py : 0;
                        int lw = int.TryParse(textLine.GetAttribute("WIDTH"), out int pw) ? pw : 0;
                        int lh = int.TryParse(textLine.GetAttribute("HEIGHT"), out int ph) ? ph : 0;

                        var lineStrings = textLine.GetElementsByTagName("String");
                        if (lineStrings.Count == 0) continue;

                        System.Text.StringBuilder lineTextBuilder = new();
                        string prevContent = string.Empty;

                        foreach (XmlElement data in lineStrings)
                        {
                            string content = data.GetAttribute("CONTENT");
                            if (string.IsNullOrEmpty(content)) continue;

                            if (lineTextBuilder.Length > 0 && !string.IsNullOrEmpty(prevContent))
                            {
                                char lastChar = prevContent[^1];
                                char firstChar = content[0];
                                if (!AppUtilities.IsCjk(lastChar) || !AppUtilities.IsCjk(firstChar))
                                    lineTextBuilder.Append(' ');
                            }
                            lineTextBuilder.Append(content);
                            prevContent = content;
                        }

                        string lineText = lineTextBuilder.ToString();
                        if (string.IsNullOrWhiteSpace(lineText)) continue;

                        double lineAngle = 0;
                        string rotAttr = textLine.GetAttribute("ROTATION");
                        if (string.IsNullOrEmpty(rotAttr))
                            rotAttr = textBlock.GetAttribute("ROTATION");

                        if (!string.IsNullOrEmpty(rotAttr) && double.TryParse(rotAttr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedRot))
                        {
                            lineAngle = parsedRot;
                        }
                        else if (lineStrings.Count >= 2)
                        {
                            XmlElement firstString = (XmlElement)lineStrings[0];
                            XmlElement lastString = (XmlElement)lineStrings[lineStrings.Count - 1];

                            if (int.TryParse(firstString.GetAttribute("HPOS"), out int x1) &&
                                int.TryParse(firstString.GetAttribute("VPOS"), out int y1) &&
                                int.TryParse(lastString.GetAttribute("HPOS"), out int x2) &&
                                int.TryParse(lastString.GetAttribute("VPOS"), out int y2))
                            {
                                double dx = x2 - x1;
                                double dy = y2 - y1;
                                if (Math.Abs(dx) > 10)
                                {
                                    double calculatedAngle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                                    if (Math.Abs(calculatedAngle) > 0.5 && Math.Abs(calculatedAngle) < 45.0)
                                    {
                                        lineAngle = calculatedAngle;
                                    }
                                }
                            }
                        }

                        int finalX = Math.Max(0, lx) - fineTune;
                        int finalY = Math.Max(0, ly) - fineTune;
                        int finalW = Math.Max(4, lw) + fineTune;
                        int finalH = Math.Max(8, lh) + fineTune;

                        var colors = OcrHelper.GetZoneAverageColors(image, finalX, finalY, finalW, finalH);

                        ImageTranslateEntry entry = new()
                        {
                            OriginalText = lineText,
                            X = finalX,
                            Y = finalY,
                            Width = finalW,
                            Height = finalH,
                            Angle = lineAngle,
                            Transform = new System.Windows.Media.RotateTransform(lineAngle),
                            SourceLanguage = App.setting.SourceLanguage,
                            TargetLanguage = App.setting.TargetLanguage,
                            FontFace = new System.Windows.Media.FontFamily(App.setting.FontFace),
                            BackgroundColor = colors?.Background,
                            ForegroundColor = colors?.Foreground,
                        };
                        entry.CalculateFontSize();
                        items.Add(entry);
                    }
                }
                else
                {
                    int x = int.TryParse(textBlock.GetAttribute("HPOS"), out int px) ? px : 0;
                    int y = int.TryParse(textBlock.GetAttribute("VPOS"), out int py) ? py : 0;
                    int width = int.TryParse(textBlock.GetAttribute("WIDTH"), out int pw) ? pw : 0;
                    int height = int.TryParse(textBlock.GetAttribute("HEIGHT"), out int ph) ? ph : 0;

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
                            if (!AppUtilities.IsCjk(lastChar) || !AppUtilities.IsCjk(firstChar))
                                fullTextBuilder.Append(' ');
                        }
                        fullTextBuilder.Append(content);
                        prevContent = content;
                    }
                    string fullTextBlock = fullTextBuilder.ToString();
                    if (string.IsNullOrWhiteSpace(fullTextBlock)) continue;

                    double lineAngle = 0;
                    string rotAttr = textBlock.GetAttribute("ROTATION");
                    if (!string.IsNullOrEmpty(rotAttr) && double.TryParse(rotAttr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedRot))
                        lineAngle = parsedRot;

                    int finalX = Math.Max(0, x) - fineTune;
                    int finalY = Math.Max(0, y) - fineTune;
                    int finalW = Math.Max(4, width) + fineTune;
                    int finalH = Math.Max(8, height) + fineTune;

                    var colors = OcrHelper.GetZoneAverageColors(image, finalX, finalY, finalW, finalH);

                    ImageTranslateEntry entry = new()
                    {
                        OriginalText = fullTextBlock,
                        X = finalX,
                        Y = finalY,
                        Width = finalW,
                        Height = finalH,
                        Angle = lineAngle,
                        Transform = new System.Windows.Media.RotateTransform(lineAngle),
                        SourceLanguage = App.setting.SourceLanguage,
                        TargetLanguage = App.setting.TargetLanguage,
                        FontFace = new System.Windows.Media.FontFamily(App.setting.FontFace),
                        BackgroundColor = colors?.Background,
                        ForegroundColor = colors?.Foreground,
                    };
                    entry.CalculateFontSize();
                    items.Add(entry);
                }
            }
            return items;
        }
    }
}
