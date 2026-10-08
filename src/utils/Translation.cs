using Porter2Stemmer;
using ScreenLookup.src.models;
using ScreenLookup.src.utils.Database;
using System.Net.Http;
using System.Text.Json;

namespace ScreenLookup.src.utils
{
    internal static class Translation
    {
        #region Fields & Initialization
        public static dynamic? TranslationProvider;
        private static readonly HttpClient ExtraMeaningsHttpClient = CreateHttpClient();
        private static readonly Dictionary<string, string> TranslatedCache = [];

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
            ChangeTranslationProvider(App.setting.TranslationProvider);
        }

        /// <summary>
        /// Changes the current translation provider service to the provider specified by providerID.
        /// </summary>
        public static void ChangeTranslationProvider(int providerID)
        {
            TranslationProvider?.Dispose();
            TranslationProvider = LanguageList.GetTranslatorService(providerID);
        }
        #endregion

        #region Translation Methods
        /// <summary>
        /// Translates the specified text asynchronously. Checks local SQLite dictionary cache first for single words.
        /// </summary>
        public static async Task<string> GetTranslated(bool isWord, string text, int sourceLang, int targetLang)
        {
            int providerID = App.setting.TranslationProvider;
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

            // Fallback translation queries using language tag getters
            if (string.IsNullOrEmpty(resultText))
            {
                Func<int, string>[] tagGetters = [
                    LanguageList.GetTesseractTagFromID,
                    LanguageList.GetLanguageISO6391FromID,
                    LanguageList.GetLanguageISO6393FromID
                ];

                for (int i = 0; i < tagGetters.Length; i++)
                {
                    try
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
                    catch (Exception ex)
                    {
                        if (i == tagGetters.Length - 1)
                        {
                            SnackbarHost.Show("Translation Error", ex.Message, SnackbarType.Error);
                            return string.Empty;
                        }
                    }
                }
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
        }

        /// <summary>
        /// Fetches dictionary alternative meanings and phonetic pronunciation asynchronously.
        /// </summary>
        public static async Task<(List<ExtraMeaningEntity> ExtraMeanings, string Phonetic)> GetExtraDetailsAsync(string text, int sourceLang, int targetLang, CancellationTokenSource token)
        {
            List<ExtraMeaningEntity> extraMeanings = [];
            string phonetic = string.Empty;
            bool isSuccess = false;

            int currentProviderIndex = App.setting.TranslationProvider;
            string providerName = (App.setting.ProviderServices != null && currentProviderIndex >= 0 && currentProviderIndex < App.setting.ProviderServices.Length)
                ? App.setting.ProviderServices[currentProviderIndex]
                : string.Empty;

            if (providerName != "Google" && providerName != "Google New")
                return (extraMeanings, phonetic);

            // Check local dictionary cache
            try
            {
                var localEntry = await DictionaryLogger.GetAsync(text, sourceLang, targetLang, currentProviderIndex);
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
                                using var response = await ExtraMeaningsHttpClient.GetAsync(requestUrl).ConfigureAwait(false);

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
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[GetExtraDetailsAsync] Endpoint failed: {ex.Message}");
                            }

                            await Task.Delay(150).ConfigureAwait(false);
                        }
                    }
                }
            }

            if (isSuccess && !token.IsCancellationRequested)
            {
                if (string.IsNullOrEmpty(phonetic))
                    phonetic = "-";

                await DictionaryLogger.SaveExtraDetailsAsync(text, sourceLang, targetLang, currentProviderIndex, extraMeanings, phonetic);
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
                System.Diagnostics.Debug.WriteLine($"[ParseGoogleDictionaryJson] Error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[ParseGooglePhoneticJson] Error: {ex.Message}");
            }
            return string.Empty;
        }
        #endregion
    }
}
