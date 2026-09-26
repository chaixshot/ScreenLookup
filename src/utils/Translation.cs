using System.Net.Http;
using System.Text.Json;

namespace ScreenLookup.src.utils
{
    public class ExtraMeaning
    {
        public string Pos { get; set; } = string.Empty;
        public List<string> Meanings { get; set; } = new();
        public string MeaningsFormatted => string.Join(", ", Meanings);
    }

    internal class Translation
    {
        public static dynamic TranslationProvider;
        private static readonly HttpClient extraMeaningsHttpClient = CreateHttpClient();

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
        /// <remarks>If a translation provider is already in use, it is disposed before switching to the
        /// new provider. This method should be called when the application needs to switch translation services at
        /// runtime.</remarks>
        /// <param name="providerID">The unique identifier of the translation provider to use. Must correspond to a valid provider supported by
        /// the application.</param>
        public static void ChangeTranslationProvider(int providerID)
        {
            TranslationProvider?.Dispose();
            TranslationProvider = LanguageList.GetTranslatorService(providerID);
        }

        /// <summary>
        /// Translates the specified text from the source language to the target language asynchronously.
        /// </summary>
        /// <param name="text">The text to translate. Cannot be null.</param>
        /// <param name="sourceLang">The identifier of the source language. Must correspond to a supported language.</param>
        /// <param name="targetLang">The identifier of the target language. Must correspond to a supported language.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the translated text, or an empty
        /// string if the translation fails.</returns>
        public static async Task<string> GetTranslated(string text, int sourceLang, int targetLang)
        {
            // Fallback 1: Tesseract Tags
            try
            {
                var translateResult = await TranslationProvider.TranslateAsync(text, LanguageList.GetTesseractTagFromID(targetLang), LanguageList.GetTesseractTagFromID(sourceLang));
                return translateResult.Translation;
            }
            catch { }

            // Fallback 2: ISO 639-1 Tags
            try
            {
                var translateResult = await TranslationProvider.TranslateAsync(text, LanguageList.GetLanguageISO6391FromID(targetLang), LanguageList.GetLanguageISO6391FromID(sourceLang));
                return translateResult.Translation;
            }
            catch { }

            // Fallback 3: ISO 639-3 Tags
            try
            {
                var translateResult = await TranslationProvider.TranslateAsync(text, LanguageList.GetLanguageISO6393FromID(targetLang), LanguageList.GetLanguageISO6393FromID(sourceLang));
                return translateResult.Translation;
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("Translation Error", ex.StackTrace, SnackbarType.Error);
                return string.Empty;
            }
        }

        /// <summary>
        /// Fetches dictionary alternative meanings / parts of speech for single words or short phrases.
        /// </summary>
        public static async Task<List<ExtraMeaning>> GetExtraMeaningsAsync(string text, int sourceLang, int targetLang)
        {
            List<ExtraMeaning> list = [];

            // Only fetch extra dictionary meanings when translationProvider is "Google" or "Google New"
            int currentProviderIndex = App.setting.TranslationProvider;
            string providerName = (App.setting.ProviderServices != null && currentProviderIndex >= 0 && currentProviderIndex < App.setting.ProviderServices.Length)
                ? App.setting.ProviderServices[currentProviderIndex]
                : string.Empty;

            if (providerName != "Google" && providerName != "Google New")
                return list;

            if (string.IsNullOrWhiteSpace(text) || (text.Trim().Contains(' ') && text.Trim().Split(' ').Length > 3))
                return list;

            string encodedText = Uri.EscapeDataString(text.Trim());

            // Language tag fallback options
            List<(string srcTag, string tgtTag)> langPairs = [
                (LanguageList.GetLanguageISO6391FromID(sourceLang), LanguageList.GetLanguageISO6391FromID(targetLang)),
                (LanguageList.GetLanguageISO6393FromID(sourceLang), LanguageList.GetLanguageISO6393FromID(targetLang)),
                (LanguageList.GetTesseractTagFromID(sourceLang), LanguageList.GetTesseractTagFromID(targetLang))
            ];

            // Google translate endpoint and client parameter fallback templates
            string[] urlTemplates = [
                "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://clients5.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://clients1.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://clients2.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://translate.google.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://translate.google.com/translate_a/single?client=webapp&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://translate.googleapis.com/translate_a/single?client=at&sl={0}&tl={1}&dt=t&dt=bd&q={2}",
                "https://translate.googleapis.com/translate_a/single?client=tw-ob&sl={0}&tl={1}&dt=t&dt=bd&q={2}"
            ];

            foreach (var (srcTag, tgtTag) in langPairs)
            {
                if (string.IsNullOrEmpty(tgtTag)) continue;
                string srcCode = string.IsNullOrEmpty(srcTag) ? "auto" : srcTag;

                foreach (string urlTemplate in urlTemplates)
                {
                    try
                    {
                        string requestUrl = string.Format(urlTemplate, srcCode, tgtTag, encodedText);
                        string json = await extraMeaningsHttpClient.GetStringAsync(requestUrl).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(json))
                        {
                            var parsed = ParseGoogleDictionaryJson(json);
                            if (parsed.Count > 0)
                                return parsed;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GetExtraMeaningsAsync] Endpoint failed: {ex.Message}");
                    }
                }
            }

            return list;
        }

        private static List<ExtraMeaning> ParseGoogleDictionaryJson(string json)
        {
            var list = new List<ExtraMeaning>();
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
                                string pos = posItem[0].GetString() ?? string.Empty;
                                var meaningsArray = posItem[1];
                                var meanings = new List<string>();

                                if (meaningsArray.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var m in meaningsArray.EnumerateArray())
                                    {
                                        string str = m.GetString() ?? string.Empty;
                                        if (!string.IsNullOrEmpty(str))
                                        {
                                            meanings.Add(str);
                                        }
                                    }
                                }

                                if (meanings.Count > 0)
                                {
                                    list.Add(new ExtraMeaning
                                    {
                                        Pos = pos,
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
    }
}
