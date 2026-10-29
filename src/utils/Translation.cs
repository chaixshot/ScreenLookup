using Porter2Stemmer;
using ScreenLookup.src.llmAPI;
using ScreenLookup.src.models;
using ScreenLookup.src.utils.Database;
using System.Text.Json;

namespace ScreenLookup.src.utils
{
    public enum ProviderService
    {
        Google,
        GoogleNew,
        Bing,
        MicrosoftAzure,
        Yandex,
        Ollama,
        LMStudio,
        OpenAI,
        OpenRouter,
        DeepL,
        Youdao,
        MTranServer,
        Baidu,
        LibreTranslate,
    }

    public static class ProviderServiceExtensions
    {
        public static string GetDisplayName(this ProviderService provider) => provider switch
        {
            ProviderService.Google => "Google",
            ProviderService.GoogleNew => "Google New",
            ProviderService.Bing => "Bing",
            ProviderService.MicrosoftAzure => "Microsoft Azure",
            ProviderService.Yandex => "Yandex",
            ProviderService.Ollama => "Ollama",
            ProviderService.LMStudio => "LMStudio",
            ProviderService.OpenAI => "OpenAI",
            ProviderService.OpenRouter => "OpenRouter",
            ProviderService.DeepL => "DeepL",
            ProviderService.Youdao => "Yodao",
            ProviderService.MTranServer => "MTranServer",
            ProviderService.Baidu => "Baidu",
            ProviderService.LibreTranslate => "LibraTranslate",
            _ => provider.ToString()
        };
    }

    public static class TranslateQueue
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public static async Task<T> EnqueueAsync<T>(Func<Task<T>> taskFactory, CancellationToken token = default)
        {
            await semaphore.WaitAsync(token);
            try
            {
                return await taskFactory();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    public static class ExtraDetailsQueue
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public static async Task<T> EnqueueAsync<T>(Func<Task<T>> taskFactory, CancellationToken token = default)
        {
            await semaphore.WaitAsync(token);
            try
            {
                return await taskFactory();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    internal static class Translation
    {
        #region Fields & Initialization
        public static dynamic? TranslationProvider;
        private static readonly HttpClient ExtraMeaningsHttpClient = CreateHttpClient();
        private static readonly Dictionary<string, string> TranslatedCache = [];
        private static readonly List<ProviderService> SupportExtraDetail = [
            ProviderService.Google,
            ProviderService.GoogleNew,
            ProviderService.Ollama,
            ProviderService.LMStudio,
            ProviderService.OpenAI,
            ProviderService.OpenRouter
        ];

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            return client;
        }

        static Translation()
        {
            ChangeTranslationProvider(App.setting.WordTranslationProvider);
        }

        /// <summary>
        /// Changes the current translation provider service to the provider specified by providerID.
        /// </summary>
        public static void ChangeTranslationProvider(ProviderService providerID)
        {
            TranslationProvider?.Dispose();
            TranslationProvider = LanguageList.GetTranslatorService(providerID);
        }
        #endregion

        #region Translation Methods
        /// <summary>
        /// Translates the specified text asynchronously. Checks local SQLite dictionary cache first for single words.
        /// </summary>
        public static async Task<string> GetTranslated(bool isWord, string text, int sourceLang, int targetLang, CancellationToken token = default)
        {
            ProviderService providerID = isWord ? App.setting.WordTranslationProvider : App.setting.ParagraphTranslationProvider;
            string resultText = string.Empty;

            // Check local dictionary for single words
            if (isWord)
            {
                var localEntry = await DictionaryLogger.GetAsync(text, sourceLang, targetLang, providerID);
                if (localEntry != null && !string.IsNullOrWhiteSpace(localEntry.Translated))
                    resultText = localEntry.Translated;
            }

            // Check in-memory cache
            if (string.IsNullOrEmpty(resultText))
            {
                if (TranslatedCache.TryGetValue(text, out string? cachedData))
                    resultText = cachedData;
            }

            // Fallback translation queries
            if (string.IsNullOrEmpty(resultText))
            {
                resultText = await TranslateQueue.EnqueueAsync(async () =>
                {
                    try
                    {
                        switch (providerID)
                        {
                            case ProviderService.Ollama:
                            case ProviderService.OpenAI:
                            case ProviderService.LMStudio:
                            case ProviderService.OpenRouter:
                            case ProviderService.DeepL:
                            case ProviderService.Youdao:
                            case ProviderService.MTranServer:
                            case ProviderService.Baidu:
                            case ProviderService.LibreTranslate:
                                resultText = await llmTranslate.GetTranslate(providerID, text, token);
                                break;
                            default:
                                {
                                    Func<int, string>[] tagGetters = [
                                        LanguageList.GetTesseractTagFromID,
                                        LanguageList.GetLanguageISO6391FromID,
                                        LanguageList.GetLanguageISO6393FromID
                                    ];

                                    for (int i = 0; i < tagGetters.Length; i++)
                                    {
                                        string targetTag = tagGetters[i](targetLang);
                                        string sourceTag = tagGetters[i](sourceLang);

                                        if (TranslationProvider != null)
                                        {
                                            var translateResult = await TranslationProvider.TranslateAsync(text, targetTag, sourceTag);
                                            if (!string.IsNullOrEmpty(translateResult.Translation))
                                            {
                                                resultText = translateResult.Translation;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return string.Empty;
                    }
                    catch (Exception ex)
                    {
                        SnackbarHost.Show("Translation Error", ex.Message, SnackbarType.Error);
                        return string.Empty;
                    }

                    // Save translated result to local dictionary
                    if (isWord && !string.IsNullOrEmpty(resultText))
                    {
                        await DictionaryLogger.SaveTranslatedAsync(text, resultText, sourceLang, targetLang, providerID);
                    }

                    if (!string.IsNullOrEmpty(resultText))
                    {
                        TranslatedCache.TryAdd(text, resultText);
                    }

                    return resultText;
                }, token);
            }

            return resultText;
        }

        /// <summary>
        /// Fetches dictionary alternative meanings and phonetic pronunciation asynchronously.
        /// </summary>
        public static async Task<(List<ExtraMeaningEntity> ExtraMeanings, string Phonetic)> GetExtraDetailsAsync(string text, int sourceLang, int targetLang, CancellationTokenSource token)
        {
            List<ExtraMeaningEntity> extraMeanings = [];
            string phonetic = string.Empty;
            bool isSuccess = false;

            if (token?.IsCancellationRequested == true)
                return (extraMeanings, phonetic);

            if (!SupportExtraDetail.Contains(App.setting.WordTranslationProvider))
                return (extraMeanings, phonetic);

            // Check local dictionary cache
            try
            {
                var localEntry = await DictionaryLogger.GetAsync(text, sourceLang, targetLang, App.setting.WordTranslationProvider);
                if (localEntry != null)
                {
                    if (localEntry.ExtraMeanings != null && localEntry.ExtraMeanings.Count > 0)
                        extraMeanings = localEntry.ExtraMeanings;

                    if (!string.IsNullOrEmpty(localEntry.Phonetic))
                        phonetic = localEntry.Phonetic;
                }
            }
            catch { }

            // API Execution if cache was missing
            if (extraMeanings.Count == 0 && string.IsNullOrEmpty(phonetic))
            {
                (extraMeanings, phonetic) = await ExtraDetailsQueue.EnqueueAsync(async () =>
                {
                    if (token?.IsCancellationRequested == true)
                        return (extraMeanings, phonetic);

                    switch (App.setting.WordTranslationProvider)
                    {
                        case ProviderService.Google:
                        case ProviderService.GoogleNew:
                            string cleanText = text.ToLower().Trim();
                            List<string> wordVariants = [cleanText];

                            if (LanguageList.GetLanguageISO6391FromID(sourceLang) == "en")
                            {
                                string stem = new EnglishPorter2Stemmer().Stem(cleanText).Value;
                                if (stem != cleanText)
                                    wordVariants.Add(stem);
                            }

                            List<(string srcTag, string tgtTag)> langPairs = [
                                (LanguageList.GetLanguageISO6391FromID(sourceLang), LanguageList.GetLanguageISO6391FromID(targetLang)),
                                (LanguageList.GetLanguageISO6393FromID(sourceLang), LanguageList.GetLanguageISO6393FromID(targetLang)),
                                (LanguageList.GetTesseractTagFromID(sourceLang), LanguageList.GetTesseractTagFromID(targetLang)),
                            ];

                            string[] urlTemplates = [
                                "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                                "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                                "https://translate.googleapis.com/translate_a/single?client=tw-ob&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm"
                            ];

                            foreach (string candidateWord in wordVariants)
                            {
                                if ((isSuccess && !string.IsNullOrEmpty(phonetic)) || token.IsCancellationRequested) break;

                                string encodedText = Uri.EscapeDataString(candidateWord);

                                foreach (var (srcTag, tgtTag) in langPairs)
                                {
                                    if ((isSuccess && !string.IsNullOrEmpty(phonetic)) || token.IsCancellationRequested) break;
                                    if (string.IsNullOrEmpty(tgtTag)) continue;
                                    string srcCode = string.IsNullOrEmpty(srcTag) ? "auto" : srcTag;

                                    foreach (string urlTemplate in urlTemplates)
                                    {
                                        if ((isSuccess && !string.IsNullOrEmpty(phonetic)) || token.IsCancellationRequested) break;

                                        try
                                        {
                                            string requestUrl = string.Format(urlTemplate, srcCode, tgtTag, encodedText);
                                            using var response = await ExtraMeaningsHttpClient.GetAsync(requestUrl, token.Token).ConfigureAwait(false);

                                            if (response.IsSuccessStatusCode)
                                            {
                                                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                                                if (!string.IsNullOrEmpty(json))
                                                {
                                                    if (extraMeanings.Count == 0 && candidateWord == cleanText)
                                                    {
                                                        var parsed = await ParseGoogleDictionaryJsonAsync(json, targetLang);
                                                        if (parsed.Count > 0)
                                                            extraMeanings = parsed;
                                                    }

                                                    if (string.IsNullOrEmpty(phonetic))
                                                    {
                                                        string parsedPhonetic = ParseGooglePhoneticJson(json);
                                                        if (!string.IsNullOrEmpty(parsedPhonetic))
                                                            phonetic = parsedPhonetic;
                                                    }

                                                    isSuccess = true;
                                                }
                                            }
                                        }
                                        catch (OperationCanceledException) { }
                                        catch (Exception ex)
                                        {
                                            SnackbarHost.Show("GetExtraDetailsAsync", $"Endpoint failed: {ex.Message}", type: SnackbarType.Error);
                                        }

                                        await Task.Delay(150).ConfigureAwait(false);
                                    }
                                }
                            }
                            break;
                        case ProviderService.Ollama:
                        case ProviderService.LMStudio:
                        case ProviderService.OpenAI:
                        case ProviderService.OpenRouter:
                            try
                            {
                                string llmResponse = await llmExtraDetails.GetExtraDetails(App.setting.WordTranslationProvider, text, token.Token);
                                if (!string.IsNullOrEmpty(llmResponse))
                                {
                                    var (ExtraMeanings, Phonetic) = await ParseLLMExtraDetailsJsonAsync(llmResponse, targetLang);
                                    if (ExtraMeanings.Count > 0)
                                        extraMeanings = ExtraMeanings;
                                    if (!string.IsNullOrEmpty(Phonetic))
                                        phonetic = Phonetic;
                                    isSuccess = true;
                                }
                            }
                            catch (OperationCanceledException) { }
                            catch (Exception ex)
                            {
                                SnackbarHost.Show("GetExtraDetailsAsync", $"LLM Endpoint failed: {ex.Message}", type: SnackbarType.Error);
                            }
                            break;

                    }

                    return (extraMeanings, phonetic);
                }, token.Token);
            }

            if (isSuccess)
            {
                if (string.IsNullOrEmpty(phonetic))
                    phonetic = "-";

                await DictionaryLogger.SaveExtraDetailsAsync(text, sourceLang, targetLang, App.setting.WordTranslationProvider, extraMeanings, phonetic);
            }

            if (string.IsNullOrEmpty(phonetic) || phonetic == "-")
                phonetic = string.Empty;
            else
                phonetic = $"/{phonetic}/";

            return (extraMeanings, phonetic);
        }
        #endregion

        #region Helper Parsing Methods
        private static async Task<List<ExtraMeaningEntity>> ParseGoogleDictionaryJsonAsync(string json, int targetLang)
        {
            var list = new List<ExtraMeaningEntity>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 1)
                {
                    var dictElement = root[1];
                    if (dictElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var posItem in dictElement.EnumerateArray())
                        {
                            if (posItem.ValueKind == JsonValueKind.Array && posItem.GetArrayLength() >= 2)
                            {
                                string wordClass = posItem[0].GetString() ?? string.Empty;
                                string translatedWordClass = await WordClassLogger.GetOrTranslateAsync(wordClass, targetLang);

                                var meaningsArray = posItem[1];
                                var meanings = new List<string>();

                                if (meaningsArray.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var m in meaningsArray.EnumerateArray())
                                    {
                                        string str = m.GetString() ?? string.Empty;
                                        if (!string.IsNullOrEmpty(str))
                                            meanings.Add(str);
                                    }
                                }

                                if (meanings.Count > 0)
                                {
                                    list.Add(new ExtraMeaningEntity
                                    {
                                        Pos = translatedWordClass,
                                        Meanings = meanings
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("ParseGoogleDictionaryJson", $"{ex.Message}", SnackbarType.Error);
            }

            return list;
        }

        private static string ParseGooglePhoneticJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var array0 = root[0];
                    if (array0.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in array0.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Array)
                            {
                                int len = item.GetArrayLength();
                                if (len >= 4 && item[3].ValueKind == JsonValueKind.String)
                                {
                                    string phonetic = item[3].GetString() ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(phonetic))
                                        return phonetic.Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("ParseGooglePhoneticJson", $"Endpoint failed: {ex.Message}", type: SnackbarType.Error);
            }
            return string.Empty;
        }

        private static string ExtractJsonSubstring(string input)
        {
            int firstBrace = input.IndexOf('{');
            int firstBracket = input.IndexOf('[');

            int start = -1;
            char openChar = '\0';
            char closeChar = '\0';

            if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
            {
                start = firstBrace;
                openChar = '{';
                closeChar = '}';
            }
            else if (firstBracket >= 0)
            {
                start = firstBracket;
                openChar = '[';
                closeChar = ']';
            }

            if (start < 0) return input;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < input.Length; i++)
            {
                char c = input[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == openChar)
                    {
                        depth++;
                    }
                    else if (c == closeChar)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return input.Substring(start, i - start + 1);
                        }
                    }
                }
            }

            int lastClose = input.LastIndexOf(closeChar);
            if (lastClose > start)
            {
                return input.Substring(start, lastClose - start + 1);
            }

            return input;
        }

        private static async Task<(List<ExtraMeaningEntity> ExtraMeanings, string Phonetic)> ParseLLMExtraDetailsJsonAsync(string json, int targetLang)
        {
            var extraMeanings = new List<ExtraMeaningEntity>();
            string phonetic = string.Empty;
            if (string.IsNullOrEmpty(json))
                return (extraMeanings, phonetic);

            try
            {
                string cleanJson = json.Replace("```json", "").Replace("```json\n", "").Replace("```", "").Trim();

                int braceIndex = cleanJson.IndexOf('{');
                while (braceIndex >= 0)
                {
                    string candidate = ExtractJsonSubstring(cleanJson.Substring(braceIndex));
                    try
                    {
                        var options = new JsonDocumentOptions
                        {
                            AllowTrailingCommas = true,
                            CommentHandling = JsonCommentHandling.Skip
                        };
                        using var doc = JsonDocument.Parse(candidate, options);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            if (root.TryGetProperty("phonetic", out var phoneticProp) && phoneticProp.ValueKind == JsonValueKind.String)
                            {
                                phonetic = phoneticProp.GetString()?.Trim() ?? string.Empty;
                            }

                            if (root.TryGetProperty("meanings", out var meaningsProp) && meaningsProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in meaningsProp.EnumerateArray())
                                {
                                    string pos = string.Empty;
                                    if (item.TryGetProperty("pos", out var posProp) && posProp.ValueKind == JsonValueKind.String)
                                    {
                                        pos = posProp.GetString() ?? string.Empty;
                                    }
                                    string translatedPos = await WordClassLogger.GetOrTranslateAsync(pos, targetLang);

                                    var meanings = new List<string>();
                                    if (item.TryGetProperty("meanings", out var mArrayProp) && mArrayProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var m in mArrayProp.EnumerateArray())
                                        {
                                            string str = m.GetString() ?? string.Empty;
                                            if (!string.IsNullOrEmpty(str))
                                                meanings.Add(str);
                                        }
                                    }
                                    else if (item.TryGetProperty("meaning", out var mStrProp) && mStrProp.ValueKind == JsonValueKind.String)
                                    {
                                        string str = mStrProp.GetString() ?? string.Empty;
                                        if (!string.IsNullOrEmpty(str))
                                            meanings.Add(str);
                                    }

                                    if (meanings.Count > 0)
                                    {
                                        extraMeanings.Add(new ExtraMeaningEntity
                                        {
                                            Pos = translatedPos,
                                            Meanings = meanings
                                        });
                                    }
                                }
                            }

                            return (extraMeanings, phonetic);
                        }
                    }
                    catch
                    {
                        // Try next candidate
                    }

                    braceIndex = cleanJson.IndexOf('{', braceIndex + 1);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ParseLLMExtraDetailsJson error: {ex.Message}");
            }
            return (extraMeanings, phonetic);
        }
        #endregion
    }
}
