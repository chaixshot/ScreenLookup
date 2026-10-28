using PaddleOCRSharp;
using ScreenLookup.src.models;
using System.Drawing;

namespace ScreenLookup.src.utils
{
    public static class PaddleHelper
    {
        public static readonly string[] LangList = [
            "abq", "afr", "ara", "ava", "aze", "bel", "bgc", "bho", "bih", "bos",
            "bul", "che", "ces", "chi_sim", "chi_tra", "cym", "dan", "dar", "deu", "eng",
            "enm", "est", "fas", "fra", "gle", "hin", "hrv", "hun", "ind", "inh",
            "isl", "ita", "jpn", "kat", "kbd", "kmr", "kok", "kor", "lat", "lav",
            "lbe", "lez", "lit", "mag", "mai", "mar", "mlt", "mon", "mri", "msa",
            "nep", "new", "nld", "nor", "oci", "pli", "pol", "por", "ron", "rus",
            "san", "sdr", "slk", "slv", "spa", "sqi", "srp", "srp_latn", "swa", "swe",
            "tab", "tam", "tel", "tgl", "tur", "uig", "ukr", "urd", "uzb", "vie"
        ];

        public static bool IsInstalled(SourceAccuracy accID)
        {
            if (App.setting.LoadedPaddle.ContainsKey($"{accID}"))
                return true;
            else
            {
                string modelDir = GetModelPath(accID);
                string detDir = Path.Combine(modelDir, "det");
                string recDir = Path.Combine(modelDir, "rec");

                // Check if both det and rec subfolders exist with valid model configuration/weights
                bool hasDet = Directory.Exists(detDir) &&
                                (File.Exists(Path.Combine(detDir, "inference.json")) || File.Exists(Path.Combine(detDir, "inference.pdmodel")) || File.Exists(Path.Combine(detDir, "model.pdmodel")));

                bool hasRec = Directory.Exists(recDir) &&
                                (File.Exists(Path.Combine(recDir, "inference.json")) || File.Exists(Path.Combine(recDir, "inference.pdmodel")) || File.Exists(Path.Combine(recDir, "model.pdmodel")));

                return Directory.Exists(modelDir) && hasDet && hasRec;
            }
        }

        public static void SaveInstalled(SourceAccuracy accID)
        {
            App.setting.LoadedPaddle[$"{accID}"] = true;
            App.setting.Save();

            App.captureWindow.LoadInstalledLanguage();
        }

        public static string GetModelPath(SourceAccuracy accID)
        {
            return accID switch
            {
                SourceAccuracy.Fast => Path.Combine(App.appDataFolder, "paddle_tiny"),
                SourceAccuracy.Normal => Path.Combine(App.appDataFolder, "paddle_small"),
                _ => Path.Combine(App.appDataFolder, "paddle_medium"),
            };
        }

        public static (OCRModelConfig Config, OCRParameter Parameter) GetModelConfig(SourceAccuracy accID)
        {
            string path = GetModelPath(accID);

            OCRParameter modelParameter = new()
            {
                use_gpu = false,                        // Whether to use GPU. This property is invalid for the free version. Defaults to true for the GPU version.
                gpu_id = 0,                             // GPU ID, valid when using GPU. On Windows, this can only be 0.
                gpu_mem = 4000,                         // Allocated GPU memory (MB)
                cpu_math_library_num_threads = 10,      // Number of threads for CPU prediction. With sufficient CPU cores, a higher value increases prediction speed.
                enable_mkldnn = true,                   // Whether to use the MKL-DNN library.
                det = true,                             // Whether to perform text detection. Single-line text can disable this parameter to increase speed.
                rec = true,                             // Whether to perform text recognition.
                cls = false,                            // Whether to perform text direction classification.
                max_side_len = 960,                     // When the input image width or height exceeds 960, it is proportionally scaled so the longest side is 960.
                det_db_thresh = 0.3f,                   // Threshold used to filter the binary image predicted by DB.
                det_db_box_thresh = 0.5f,               // Threshold for filtering boxes in DB post-processing.
                det_db_unclip_ratio = 1.6f,             // Represents the tightness of the text box. Smaller values make the text box closer to the text.
                use_dilation = false,                   // Whether to use dilation on the output map.
                det_db_score_mode = true,               // true: Uses polygon boxes to calculate bbox score; false: Uses rectangle boxes.
                visualize = false,                      // Whether to visualize the results (saves ocr_vis.png in the current directory).
                use_angle_cls = false,                  // Whether to use the direction classifier. Deprecated, use cls instead.
                cls_thresh = 0.9f,                      // Score threshold for the direction classifier.
                cls_batch_num = 1,                      // Batch size for the direction classifier.
                rec_batch_num = 6,                      // Batch size for the recognition model. Increasing this appropriately can increase speed.
                rec_img_h = 48,                         // Input image height for the recognition model. (Note: often named rec_img_h depending on your PaddleOCRSharp version)
                rec_img_w = 320,                        // Input image width for the recognition model.
                show_img_vis = false,                   // Whether to display the prediction results visually.
                use_tensorrt = false                    // Whether to enable TensorRT when making GPU predictions.
            };

            OCRModelConfig modelConfig = new()
            {
                det_infer = Path.Combine(path, "det"),
                cls_infer = Path.Combine(path, "cls"),
                rec_infer = Path.Combine(path, "rec"),
                keys = Path.Combine(path, "ppocr_keys.txt")
            };

            return (modelConfig, modelParameter);
        }

        public static async Task<bool> InstallModelAsync(SourceAccuracy accID)
        {
            if (IsInstalled(accID)) return true;

            string targetDir = GetModelPath(accID);
            Directory.CreateDirectory(targetDir);

            string tier = accID switch
            {
                SourceAccuracy.Fast => "tiny",
                SourceAccuracy.Normal => "small",
                _ => "medium"
            };

            string[] modelTypes = { "det", "rec" };
            using HttpClient client = new();

            foreach (string type in modelTypes)
            {
                string subDir = Path.Combine(targetDir, type);
                string repoName = $"PP-OCRv6_{tier}_{type}"; // ModelScope direct raw file URLs for PP-OCRv6 components (ModelScope hosts files directly on cloud buckets accessible via static paths or API)
                string[] filesToDownload = ["model.pdmodel", "model.pdiparams", "inference.pdmodel", "inference.pdiparams", "inference.json", "inference.yml", "dict.txt", "ppocr_keys.txt"]; // List of core files needed for Paddle inference
                bool successAny = false;

                Directory.CreateDirectory(subDir);
                SnackbarHost.Show("Paddle", $"Downloading model {repoName}...", SnackbarType.Info, timeout: 99999, closeButton: false);

                foreach (string fileName in filesToDownload)
                {
                    string fileUrl = $"https://www.modelscope.cn/api/v1/models/PaddlePaddle/{repoName}/repo?FilePath={fileName}";
                    string destPath = Path.Combine(subDir, fileName);

                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(fileUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            if (data.Length > 500) // Ensure it's not an LFS pointer or 404 HTML page (< 1KB error / pointer check)
                            {
                                await File.WriteAllBytesAsync(destPath, data);
                                successAny = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SnackbarHost.Show("PaddleHelper", $"Skipping {fileName}: {ex.Message}", type: SnackbarType.Error);
                    }
                }

                if (!successAny)
                    return false;
            }

            return true;
        }

        public static Task<OCRResult?> RunOcrAsync(PaddleOCREngine engine, Bitmap image)
        {
            if (engine == null) return Task.FromResult<OCRResult?>(null);
            var result = engine.DetectText(image);
            return Task.FromResult<OCRResult?>(result);
        }

        public static List<CaptureWordsSimplifiedEntry> CaptureWordsSimplify(OCRResult? result)
        {
            List<CaptureWordsSimplifiedEntry> items = [];
            if (result?.TextBlocks == null || result.TextBlocks.Count == 0) return items;

            foreach (var block in result.TextBlocks)
            {
                if (string.IsNullOrWhiteSpace(block.Text)) continue;

                List<string> words = [];
                if (block.Text.Any(AppUtilities.IsCjk))
                {
                    System.Text.StringBuilder sb = new();
                    foreach (char c in block.Text)
                    {
                        if (char.IsWhiteSpace(c))
                        {
                            if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
                        }
                        else if (AppUtilities.IsCjk(c))
                        {
                            if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
                            words.Add(c.ToString());
                        }
                        else
                        {
                            sb.Append(c);
                        }
                    }
                    if (sb.Length > 0) words.Add(sb.ToString());
                }
                else
                {
                    words.AddRange(block.Text.Split([' '], StringSplitOptions.RemoveEmptyEntries));
                }

                foreach (string rawWord in words)
                {
                    string word = rawWord;
                    if (App.setting.HunSpell)
                        word = HunspellHelper.CorrectionWord(word);

                    items.Add(new CaptureWordsSimplifiedEntry { Word = word, Stop = 0 });
                }
                items.Add(new CaptureWordsSimplifiedEntry { Word = string.Empty, Stop = 1 });
            }
            items.Add(new CaptureWordsSimplifiedEntry { Word = string.Empty, Stop = 3 });

            return items;
        }

        public static string FormatTextLayout(OCRResult? result)
        {
            if (result?.TextBlocks == null || result.TextBlocks.Count == 0)
                return (result?.Text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

            // Calculate average rotation angle across text blocks to un-rotate coordinates for layout formatting
            double totalAngleDeg = 0;
            int angleCount = 0;

            foreach (var block in result.TextBlocks)
            {
                if (block.BoxPoints != null && block.BoxPoints.Count >= 4)
                {
                    double cx = block.BoxPoints.Take(4).Average(p => p.X);
                    double cy = block.BoxPoints.Take(4).Average(p => p.Y);

                    var polarOrdered = block.BoxPoints.Take(4)
                        .Select(p => new { Point = p, Angle = Math.Atan2(p.Y - cy, p.X - cx) })
                        .OrderBy(p => p.Angle)
                        .Select(p => p.Point)
                        .ToList();

                    int tlIndex = 0;
                    double minScore = double.MaxValue;
                    for (int i = 0; i < 4; i++)
                    {
                        double score = polarOrdered[i].X + polarOrdered[i].Y;
                        if (score < minScore)
                        {
                            minScore = score;
                            tlIndex = i;
                        }
                    }

                    PointF pTL = new(polarOrdered[tlIndex].X, polarOrdered[tlIndex].Y);
                    PointF pTR = new(polarOrdered[(tlIndex + 1) % 4].X, polarOrdered[(tlIndex + 1) % 4].Y);

                    float topVx = pTR.X - pTL.X;
                    float topVy = pTR.Y - pTL.Y;
                    double angleRad = Math.Atan2(topVy, topVx);
                    double angleDeg = angleRad * (180.0 / Math.PI);

                    totalAngleDeg += angleDeg;
                    angleCount++;
                }
            }

            double avgAngleDeg = angleCount > 0 ? totalAngleDeg / angleCount : 0.0;
            double avgAngleRad = avgAngleDeg * (Math.PI / 180.0);

            double cosA = Math.Cos(-avgAngleRad);
            double sinA = Math.Sin(-avgAngleRad);

            var blocks = result.TextBlocks
                .Where(b => !string.IsNullOrWhiteSpace(b.Text))
                .Select((b, index) =>
                {
                    double minX = 0;
                    double maxX = 100;
                    double minY = index * 15.0;
                    double maxY = (index + 1) * 15.0;
                    double height = 15.0;
                    double width = 100.0;

                    if (b.BoxPoints != null && b.BoxPoints.Count > 0)
                    {
                        var rotatedPoints = b.BoxPoints.Select(p => new
                        {
                            X = p.X * cosA - p.Y * sinA,
                            Y = p.X * sinA + p.Y * cosA
                        }).ToList();

                        minX = rotatedPoints.Min(p => p.X);
                        maxX = rotatedPoints.Max(p => p.X);
                        minY = rotatedPoints.Min(p => p.Y);
                        maxY = rotatedPoints.Max(p => p.Y);
                        height = Math.Max(1.0, maxY - minY);
                        width = Math.Max(1.0, maxX - minX);
                    }

                    return new
                    {
                        Block = b,
                        Index = index,
                        MinX = minX,
                        MaxX = maxX,
                        MinY = minY,
                        MaxY = maxY,
                        Height = height,
                        Width = width,
                        Text = b.Text.Trim()
                    };
                })
                .OrderBy(b => b.MinY)
                .ThenBy(b => b.Index)
                .ToList();

            if (blocks.Count == 0) return (result.Text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

            // Group blocks into visual lines based on Y overlap
            List<List<dynamic>> visualLines = [];

            foreach (var b in blocks)
            {
                bool placed = false;
                foreach (var line in visualLines)
                {
                    double avgMinY = line.Average(item => (double)item.MinY);
                    double avgHeight = line.Average(item => (double)item.Height);

                    if (Math.Abs(b.MinY - avgMinY) < avgHeight * 0.5)
                    {
                        line.Add(b);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    visualLines.Add([b]);
                }
            }

            visualLines = visualLines.OrderBy(line => line.Average(item => (double)item.MinY)).ToList();

            System.Text.StringBuilder pageTextBuilder = new();

            for (int l = 0; l < visualLines.Count; l++)
            {
                var lineBlocks = visualLines[l]
                    .OrderBy(b => (double)b.MinX)
                    .ToList();

                if (l > 0)
                {
                    var prevLine = visualLines[l - 1];
                    double prevMaxY = prevLine.Max(b => (double)b.MaxY);
                    double currentMinY = lineBlocks.Min(b => (double)b.MinY);
                    double avgHeight = lineBlocks.Average(b => (double)b.Height);

                    double verticalGap = currentMinY - prevMaxY;
                    if (verticalGap > avgHeight * 0.3)
                    {
                        pageTextBuilder.Append("\n\n"); // Double newline for paragraph/heading gap
                    }
                    else
                    {
                        pageTextBuilder.Append("\n"); // Single newline for line break
                    }
                }

                for (int bIdx = 0; bIdx < lineBlocks.Count; bIdx++)
                {
                    var currentBlock = lineBlocks[bIdx];

                    if (bIdx > 0)
                    {
                        var prevBlock = lineBlocks[bIdx - 1];
                        double gap = currentBlock.MinX - prevBlock.MaxX;

                        double charWidth = prevBlock.Text.Length > 0
                            ? (prevBlock.Width / prevBlock.Text.Length)
                            : 10.0;

                        if (gap >= charWidth * 3.5)
                        {
                            pageTextBuilder.Append("\t\t");
                        }
                        else if (gap >= charWidth * 2.0)
                        {
                            pageTextBuilder.Append("\t");
                        }
                        else if (gap >= charWidth * 1.0)
                        {
                            pageTextBuilder.Append("  ");
                        }
                        else
                        {
                            pageTextBuilder.Append(' ');
                        }
                    }

                    string blockText = currentBlock.Text.Replace("\r\n", "\n").Replace("\r", "\n");
                    string[] blockLines = blockText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    for (int bl = 0; bl < blockLines.Length; bl++)
                    {
                        if (bl > 0)
                        {
                            pageTextBuilder.Append("\n");
                        }
                        pageTextBuilder.Append(blockLines[bl].Trim());
                    }
                }
            }

            string formattedText = pageTextBuilder.ToString().Trim().Replace("\r\n", "\n").Replace("\r", "\n");
            return string.IsNullOrEmpty(formattedText) ? (result.Text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n") : formattedText;
        }

        public static List<CaptureAltoEntry> AltoTextProcess(OCRResult? result, Bitmap image)
        {
            List<CaptureAltoEntry> items = [];
            if (result?.TextBlocks == null || result.TextBlocks.Count == 0) return items;

            using Graphics g = Graphics.FromImage(image);
            using Font measureFont = new(System.Drawing.FontFamily.GenericSansSerif, 12f);
            using StringFormat sf = new(StringFormat.GenericTypographic);

            // Measure true space width in GDI+
            float spaceWidth = g.MeasureString("a a", measureFont, 1000, sf).Width - g.MeasureString("aa", measureFont, 1000, sf).Width;
            if (spaceWidth < 1f) spaceWidth = 3f;

            foreach (var block in result.TextBlocks)
            {
                if (string.IsNullOrWhiteSpace(block.Text) || block.BoxPoints == null || block.BoxPoints.Count < 4)
                    continue;

                // Order 4 corner points topologically: Top-Left, Top-Right, Bottom-Right, Bottom-Left
                double cx = block.BoxPoints.Take(4).Average(p => p.X);
                double cy = block.BoxPoints.Take(4).Average(p => p.Y);

                var polarOrdered = block.BoxPoints.Take(4)
                    .Select(p => new { Point = p, Angle = Math.Atan2(p.Y - cy, p.X - cx) })
                    .OrderBy(p => p.Angle)
                    .Select(p => p.Point)
                    .ToList();

                int tlIndex = 0;
                double minScore = double.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    double score = polarOrdered[i].X + polarOrdered[i].Y;
                    if (score < minScore)
                    {
                        minScore = score;
                        tlIndex = i;
                    }
                }

                PointF pTL = new(polarOrdered[tlIndex].X, polarOrdered[tlIndex].Y);
                PointF pTR = new(polarOrdered[(tlIndex + 1) % 4].X, polarOrdered[(tlIndex + 1) % 4].Y);
                PointF pBR = new(polarOrdered[(tlIndex + 2) % 4].X, polarOrdered[(tlIndex + 2) % 4].Y);
                PointF pBL = new(polarOrdered[(tlIndex + 3) % 4].X, polarOrdered[(tlIndex + 3) % 4].Y);

                // Top edge vector (from TL to TR)
                float topVx = pTR.X - pTL.X;
                float topVy = pTR.Y - pTL.Y;
                double topLen = Math.Sqrt(topVx * topVx + topVy * topVy);

                // Left edge vector (from TL to BL)
                float leftVx = pBL.X - pTL.X;
                float leftVy = pBL.Y - pTL.Y;
                double leftLen = Math.Sqrt(leftVx * leftVx + leftVy * leftVy);

                if (topLen < 1.0) topLen = 1.0;
                if (leftLen < 1.0) leftLen = 1.0;

                // Unit vectors along top and left edges
                double uTopX = topVx / topLen;
                double uTopY = topVy / topLen;

                double uLeftX = leftVx / leftLen;
                double uLeftY = leftVy / leftLen;

                // Rotation angle of top edge in degrees
                double angleRad = Math.Atan2(topVy, topVx);
                double angleDeg = angleRad * (180.0 / Math.PI);

                // Split text block into lines (handles multi-line text blocks)
                string[] rawLines = block.Text.Split(["\n"], StringSplitOptions.RemoveEmptyEntries);
                List<string> textLines = rawLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                if (textLines.Count == 0) textLines.Add(block.Text);

                int lineCount = textLines.Count;
                bool isVertical = leftLen > topLen * 1.5;

                double lineThickness = isVertical ? (topLen / lineCount) : (leftLen / lineCount);

                // Precompute character widths per line to proportionally scale line length relative to longest line
                double[] lineCharWidthSums = new double[lineCount];
                List<double[]> lineCharWidthArrays = [];

                for (int l = 0; l < lineCount; l++)
                {
                    string lText = textLines[l];
                    double[] cWidths = new double[lText.Length];
                    double sumW = 0;
                    for (int c = 0; c < lText.Length; c++)
                    {
                        char ch = lText[c];
                        if (char.IsWhiteSpace(ch))
                        {
                            cWidths[c] = spaceWidth;
                        }
                        else
                        {
                            var sz = g.MeasureString(ch.ToString(), measureFont, 1000, sf);
                            cWidths[c] = isVertical ? sz.Height : sz.Width;
                            if (cWidths[c] < 1.0) cWidths[c] = 1.0;
                        }
                        sumW += cWidths[c];
                    }
                    lineCharWidthSums[l] = sumW > 0 ? sumW : 1.0;
                    lineCharWidthArrays.Add(cWidths);
                }

                double maxLineCharWidth = lineCharWidthSums.Max();
                if (maxLineCharWidth <= 0) maxLineCharWidth = 1.0;

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

                for (int lineIdx = 0; lineIdx < lineCount; lineIdx++)
                {
                    string lineText = textLines[lineIdx];
                    double lineTextWidthSum = lineCharWidthSums[lineIdx];
                    double[] charWidths = lineCharWidthArrays[lineIdx];

                    // Scale line target length so short lines (like "[9]") don't stretch across the full block width
                    double lineLengthRatio = Math.Min(1.0, lineTextWidthSum / maxLineCharWidth);
                    double fullBlockLength = isVertical ? leftLen : topLen;
                    double lineTargetLength = fullBlockLength * lineLengthRatio;

                    // Origin for current line within the text block
                    double lineOffset = lineIdx * lineThickness;
                    PointF lineOrigin = isVertical
                        ? new PointF((float)(pTL.X + lineOffset * uTopX), (float)(pTL.Y + lineOffset * uTopY))
                        : new PointF((float)(pTL.X + lineOffset * uLeftX), (float)(pTL.Y + lineOffset * uLeftY));

                    // Apply padding shift upwards/leftwards
                    double padY = 2.0;
                    PointF pTL_line_padded = new(
                        (float)(lineOrigin.X - padY * uLeftX),
                        (float)(lineOrigin.Y - padY * uLeftY)
                    );

                    // Extract word spans (word text, start index, length)
                    List<(string word, int startIdx, int len)> wordSpans = [];

                    if (lineText.Any(AppUtilities.IsCjk))
                    {
                        System.Text.StringBuilder sb = new();
                        int sbStart = -1;

                        for (int c = 0; c < lineText.Length; c++)
                        {
                            char ch = lineText[c];
                            if (char.IsWhiteSpace(ch))
                            {
                                if (sb.Length > 0)
                                {
                                    wordSpans.Add((sb.ToString(), sbStart, sb.Length));
                                    sb.Clear();
                                    sbStart = -1;
                                }
                            }
                            else if (AppUtilities.IsCjk(ch))
                            {
                                if (sb.Length > 0)
                                {
                                    wordSpans.Add((sb.ToString(), sbStart, sb.Length));
                                    sb.Clear();
                                    sbStart = -1;
                                }
                                wordSpans.Add((ch.ToString(), c, 1));
                            }
                            else
                            {
                                if (sb.Length == 0) sbStart = c;
                                sb.Append(ch);
                            }
                        }
                        if (sb.Length > 0)
                        {
                            wordSpans.Add((sb.ToString(), sbStart, sb.Length));
                        }
                    }
                    else
                    {
                        int c = 0;
                        while (c < lineText.Length)
                        {
                            while (c < lineText.Length && char.IsWhiteSpace(lineText[c])) c++;
                            if (c >= lineText.Length) break;

                            int start = c;
                            while (c < lineText.Length && !char.IsWhiteSpace(lineText[c])) c++;

                            string w = lineText.Substring(start, c - start);
                            wordSpans.Add((w, start, c - start));
                        }
                    }

                    if (wordSpans.Count == 0) continue;

                    double[] charStartPos = new double[lineText.Length + 1];
                    double totalCharWidth = 0;
                    for (int c = 0; c < lineText.Length; c++)
                    {
                        charStartPos[c] = totalCharWidth;
                        totalCharWidth += charWidths[c];
                    }
                    charStartPos[lineText.Length] = totalCharWidth;

                    if (totalCharWidth <= 0) totalCharWidth = 1.0;

                    for (int i = 0; i < wordSpans.Count; i++)
                    {
                        var span = wordSpans[i];
                        string rawWord = span.word;
                        string word = rawWord;
                        if (App.setting.HunSpell)
                            word = HunspellHelper.CorrectionWord(word);

                        double startFrac = charStartPos[span.startIdx] / totalCharWidth;
                        double endFrac = charStartPos[span.startIdx + span.len] / totalCharWidth;

                        double posStart = startFrac * lineTargetLength;
                        double posEnd = endFrac * lineTargetLength;
                        double wordLen = Math.Max(4.0, posEnd - posStart);

                        if (isVertical)
                        {
                            double posX = pTL_line_padded.X + posStart * uLeftX;
                            double posY = pTL_line_padded.Y + posStart * uLeftY;

                            items.Add(new CaptureAltoEntry
                            {
                                Word = word,
                                X = Math.Max(0, posX),
                                Y = Math.Max(0, posY),
                                Width = Math.Max(4, lineThickness),
                                Height = wordLen,
                                Angle = angleDeg,
                                SourceLanguage = App.setting.SourceLanguage,
                                TargetLanguage = App.setting.TargetLanguage,
                                Uid = block.Text,
                                WordOccurrenceIndex = GetOccurrenceIndex(word),
                            });
                        }
                        else
                        {
                            double posX = pTL_line_padded.X + posStart * uTopX;
                            double posY = pTL_line_padded.Y + posStart * uTopY;

                            items.Add(new CaptureAltoEntry
                            {
                                Word = word,
                                X = Math.Max(0, posX),
                                Y = Math.Max(0, posY),
                                Width = wordLen,
                                Height = Math.Max(8, lineThickness + 4.0),
                                Angle = angleDeg,
                                SourceLanguage = App.setting.SourceLanguage,
                                TargetLanguage = App.setting.TargetLanguage,
                                Uid = block.Text,
                                WordOccurrenceIndex = GetOccurrenceIndex(word),
                            });
                        }
                    }
                }
            }

            return items;
        }
    }
}