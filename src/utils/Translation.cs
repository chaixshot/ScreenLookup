using Porter2Stemmer;
using ScreenLookup.src.models;
using System.Net.Http;
using System.Text.Json;

namespace ScreenLookup.src.utils
{
    internal class Translation
    {
        public static dynamic TranslationProvider;
        private static readonly HttpClient extraMeaningsHttpClient = CreateHttpClient();
        private static readonly Dictionary<string, string> translatedCache = [];

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
        /// Changes the current translation provider to the provider specified by the given identifier.
        /// </summary>
        /// <remarks>
        /// If a translation provider is already in use, it is disposed before switching to the new provider. This
        /// method should be called when the application needs to switch translation services at runtime.
        /// </remarks>
        /// <param name="providerID">
        /// The unique identifier of the translation provider to use. Must correspond to a valid provider supported by
        /// the application.
        /// </param>
        public static void ChangeTranslationProvider(int providerID)
        {
            TranslationProvider?.Dispose();
            TranslationProvider = LanguageList.GetTranslatorService(providerID);
        }

        /// <summary>
        /// Translates the specified text from the source language to the target language asynchronously. Checks local
        /// SQLite dictionary cache first for single words / short phrases before hitting external APIs.
        /// </summary>
        /// <param name="text">The text to translate. Cannot be null.</param>
        /// <param name="sourceLang">
        /// The identifier of the source language. Must correspond to a supported language.
        /// </param>
        /// <param name="targetLang">
        /// The identifier of the target language. Must correspond to a supported language.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the translated text, or an empty
        /// string if the translation fails.
        /// </returns>
        public static async Task<string> GetTranslated(bool isWord, string text, int sourceLang, int targetLang)
        {
            int providerID = App.setting.TranslationProvider;
            string resultText = string.Empty;

            // Check local dictionary
            if (isWord)
            {
                var localEntry = await DictionaryLogger.GetAsync(text, sourceLang, targetLang, providerID);
                if (localEntry != null && !string.IsNullOrWhiteSpace(localEntry.Translated))
                    resultText = localEntry.Translated;
            }

            // Check cache
            if (string.IsNullOrEmpty(resultText))
                if (translatedCache.TryGetValue(text, out string cachedData))
                    resultText = cachedData;

            // Fallbacks via tag type loop
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
                        var translateResult = await TranslationProvider.TranslateAsync(text, targetTag, sourceTag);

                        if (!string.IsNullOrEmpty(translateResult.Translation))
                        {
                            resultText = translateResult.Translation;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Show snackbar and exit early only on the final fallback failure
                        if (i == tagGetters.Length - 1)
                        {
                            SnackbarHost.Show("Translation Error", ex.StackTrace, SnackbarType.Error);
                            return string.Empty;
                        }
                    }
                }
            }

            // Save translated result to local dictionary
            if (isWord && !string.IsNullOrEmpty(resultText))
                await DictionaryLogger.SaveTranslatedAsync(text, resultText, sourceLang, targetLang, providerID);

            translatedCache.TryAdd(text, resultText);
            return resultText;
        }

        /// <summary>
        /// Fetches dictionary alternative meanings and phonetic pronunciation in a single workflow. Checks local SQLite
        /// dictionary cache first before querying Google Translate APIs.
        /// </summary>
        public static async Task<(List<ExtraMeaningEntity> ExtraMeanings, string Phonetic)> GetExtraDetailsAsync(string text, int sourceLang, int targetLang, CancellationTokenSource token)
        {
            List<ExtraMeaningEntity> extraMeanings = [];
            string phonetic = string.Empty;
            bool isSuccess = false;

            // Only fetch extra dictionary meanings / phonetics when translationProvider is "Google" or "Google New"
            int currentProviderIndex = App.setting.TranslationProvider;
            string providerName = (App.setting.ProviderServices != null && currentProviderIndex >= 0 && currentProviderIndex < App.setting.ProviderServices.Length)
                ? App.setting.ProviderServices[currentProviderIndex]
                : string.Empty;

            if (providerName != "Google" && providerName != "Google New")
                return (extraMeanings, phonetic);

            // Check local dictionary
            try
            {
                var localEntry = await DictionaryLogger.GetAsync(text, sourceLang, targetLang, currentProviderIndex);

                if (localEntry != null)
                {
                    if (localEntry.ExtraMeanings != null && localEntry.ExtraMeanings.Count > 0)
                        extraMeanings = localEntry.ExtraMeanings;

                    if (localEntry.Phonetic != null)
                        phonetic = localEntry.Phonetic;
                }
            }
            catch { }

            // API Execution loops
            if (extraMeanings.Count == 0 && string.IsNullOrEmpty(phonetic))
            {
                // Setup text variants for phonetic lookup stem fallbacks
                string cleanText = text.ToLower().Trim();
                List<string> wordVariants = [cleanText];

                // Handle suffix variations safely,  e.g. "walking" -> "walk",  "making" -> "make", "powered" -> "power", "stopped" -> "stopp" -> "stop"
                if (LanguageList.GetLanguageISO6391FromID(sourceLang) == "en")
                {
                    string stem = new EnglishPorter2Stemmer().Stem(cleanText).Value;
                    if (stem != cleanText)
                        wordVariants.Add(stem);
                }

                // Language tag fallback options
                List<(string srcTag, string tgtTag)> langPairs = [
                    (LanguageList.GetLanguageISO6391FromID(sourceLang), LanguageList.GetLanguageISO6391FromID(targetLang)),
                    (LanguageList.GetLanguageISO6393FromID(sourceLang), LanguageList.GetLanguageISO6393FromID(targetLang)),
                    (LanguageList.GetTesseractTagFromID(sourceLang), LanguageList.GetTesseractTagFromID(targetLang)),
                ];

                // Google translate endpoint and client parameter fallback templates requesting both dt=bd and dt=rm
                string[] urlTemplates = [
                    "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    //"https://clients5.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    //"https://clients1.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    //"https://clients2.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    //"https://translate.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    //"https://translate.google.com/translate_a/single?client=webapp&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    //"https://translate.googleapis.com/translate_a/single?client=at&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm",
                    "https://translate.googleapis.com/translate_a/single?client=tw-ob&sl={0}&tl={1}&dt=t&q={2}&dt=bd&dt=rm"
                ];

                foreach (string candidateWord in wordVariants)
                {
                    // Stop outer loop if both extra meanings and phonetics have been resolved
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
                                using var response = await extraMeaningsHttpClient.GetAsync(requestUrl).ConfigureAwait(false);

                                if (response.IsSuccessStatusCode)
                                {
                                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                                    if (!string.IsNullOrEmpty(json))
                                    {
                                        // Fetch extra meanings only if not found yet (uses base/original word candidate)
                                        if (extraMeanings.Count == 0 && candidateWord == cleanText)
                                        {
                                            var parsed = await ParseGoogleDictionaryJsonAsync(json, targetLang);
                                            if (parsed.Count > 0)
                                                extraMeanings = parsed;
                                        }

                                        // Fetch phonetics if not found yet
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
                                System.Diagnostics.Debug.WriteLine($"[GetExtraMeaningsAndPhoneticAsync] Endpoint failed: {ex.Message}");
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

                await DictionaryLogger.SaveExtraDetailssAsync(text, sourceLang, targetLang, currentProviderIndex, extraMeanings, phonetic);
            }

            if (string.IsNullOrEmpty(phonetic) || phonetic == "-")
                phonetic = string.Empty;
            else
                phonetic = $"/{phonetic}/";

            return (extraMeanings, phonetic);
        }

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
                                // Normalize POS string to NFC and translate word class
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
                                // Index 3 is specifically the source language phonetic / romanization
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
    }
}
